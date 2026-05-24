using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Result<int>>
{
    private const int MaxLevel = 5;

    private readonly IAccountingDbContext _db;
    public CreateAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result<int>> Handle(CreateAccountCommand req, CancellationToken ct)
    {
        try
        {
            var code = (req.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Result.Failure<int>("رمز الحساب مطلوب");
            if (string.IsNullOrWhiteSpace(req.NameAr))
                return Result.Failure<int>("اسم الحساب مطلوب");

            var nameAr = req.NameAr.Trim();

            // ‎تحقّق من تفرّد الكود — قد يكون محجوزاً بحساب معطَّل (IsActive=false)
            // ‎أو محذوف ناعماً (IsDeleted=true في سلة المهملات). كلاهما مخفي من شاشات
            // ‎الاختيار، لكن قيد الفريد على عمود Code في DB لا يفرّق بينهم وقد يرفض
            // ‎الإدخال بخطأ SQL خام. لذلك نفحص أولاً بـ IgnoreQueryFilters ونعطي رسالة
            // ‎واضحة تُرشد المستخدم إلى الإجراء الصحيح (تفعيل/استعادة من السلة).
            var existing = await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.Code == code)
                .Select(a => new { a.Id, a.NameAr, a.IsActive, a.IsDeleted })
                .FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                string note;
                if (existing.IsDeleted)
                    note = $" — يوجد حساب محذوف في السلة بنفس الكود (\"{existing.NameAr}\"). " +
                           $"يمكنك استعادته من سلة المهملات بدلاً من إنشاء جديد.";
                else if (!existing.IsActive)
                    note = $" — يوجد حساب معطَّل بنفس الكود (\"{existing.NameAr}\"). " +
                           $"يمكنك تفعيله من قائمة الحسابات بدلاً من إنشاء جديد.";
                else
                    note = string.Empty;
                return Result.Failure<int>($"رمز الحساب '{code}' مستخدم بالفعل{note}");
            }

            // ‎تحقّق من تفرّد الاسم بين الإخوة (siblings تحت نفس الأب). نقتطع المسافات
            // ‎ونفحص الحسابات المفعَّلة والمعطَّلة معاً لتفادي الالتباس عند تفعيل حساب لاحقاً.
            var parentIdForNameCheck = req.ParentId;
            var dupName = await _db.Accounts.AsNoTracking()
                .AnyAsync(a => a.ParentId == parentIdForNameCheck && a.NameAr == nameAr, ct);
            if (dupName)
                return Result.Failure<int>(
                    $"الاسم '{nameAr}' مستخدم بالفعل تحت نفس الأب — استخدم اسماً مختلفاً");

            int level = 1;
            Account? parent = null;
            if (req.ParentId.HasValue)
            {
                parent = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.ParentId.Value, ct);
                if (parent is null)
                    return Result.Failure<int>("الحساب الأب غير موجود");
                level = parent.Level + 1;

                // ‎إذا كان الأب ورقة (Leaf) ومستخدم فعلاً (قيود/صناديق/أنواع سندات/رصيد
                // ‎افتتاحي) — لا نسمح بإضافة فروع تحته، لأن إضافة فرع تحوّله إلى أب
                // ‎(non-leaf) فيكسر القيود/المراجع التي تشير إليه كحساب تفصيلي.
                if (parent.IsLeaf)
                {
                    var pid = parent.Id;
                    var inUse = await _db.JournalEntryLines.AnyAsync(l => l.AccountId == pid, ct)
                        || await _db.CashBoxes.AnyAsync(b => b.AccountId == pid, ct)
                        || await _db.JournalVoucherTypes.AnyAsync(
                            v => v.DefaultDebitAccountId == pid || v.DefaultCreditAccountId == pid, ct)
                        || parent.OpeningBalance != 0;
                    if (inUse)
                        return Result.Failure<int>(
                            "لا يمكن إضافة حساب فرعي تحت حساب مرتبط بقيد أو نافذة (صندوق/نوع سند/رصيد افتتاحي). " +
                            "أنشئ الفرع تحت حساب أب آخر، أو قم بإلغاء الارتباط أولاً.");
                }
            }

            if (level > MaxLevel)
                return Result.Failure<int>($"لا يمكن إنشاء حسابات أعمق من المستوى {MaxLevel}");

            var nature = req.Nature ?? Account.GetDefaultNature(req.Type);
            // إذا كان للحساب الأب أبناء، يجب أن يكون non-leaf — نضمن ذلك
            if (parent is not null && parent.IsLeaf)
                parent.MarkAsLeaf(false);

            var account = Account.Create(code, req.NameAr, req.Type, nature,
                req.ParentId, level, req.IsLeaf || level == MaxLevel);
            if (!string.IsNullOrWhiteSpace(req.Description) || !string.IsNullOrWhiteSpace(req.NameEn))
                account.UpdateBasic(req.NameAr, req.NameEn, req.Description);

            await _db.Accounts.AddAsync(account, ct);
            await _db.SaveChangesAsync(ct);
            return Result.Success(account.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
