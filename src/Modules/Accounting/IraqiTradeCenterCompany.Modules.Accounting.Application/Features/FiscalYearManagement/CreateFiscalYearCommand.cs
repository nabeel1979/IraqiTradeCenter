using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record CreateFiscalYearCommand(string Name, DateTime StartDate, DateTime EndDate) : IRequest<int>;

public class CreateFiscalYearHandler : IRequestHandler<CreateFiscalYearCommand, int>
{
    private readonly IAccountingDbContext _db;
    public CreateFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<int> Handle(CreateFiscalYearCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainException("اسم السنة المالية مطلوب");

        var trimmed = req.Name.Trim();

        // ‎فحص الاسم المكرر أولاً — DB فيها unique index على Name، فبدون هذا
        // ‎الفحص يُلقى DbUpdateException عاماً (يصل للمستخدم كرسالة "فشل الإنشاء"
        // ‎بدون تفاصيل). الفحص هنا يُحوّله إلى رسالة واضحة باللغة العربية.
        var dupName = await _db.FiscalYears.AsNoTracking()
            .AnyAsync(f => f.Name == trimmed, ct);
        if (dupName)
            throw new DomainException(
                $"يوجد سنة مالية أخرى بنفس الاسم: \"{trimmed}\". اختر اسماً مختلفاً.");

        var overlap = await _db.FiscalYears.AsNoTracking().AnyAsync(f =>
            (req.StartDate >= f.StartDate && req.StartDate <= f.EndDate) ||
            (req.EndDate >= f.StartDate && req.EndDate <= f.EndDate) ||
            (req.StartDate <= f.StartDate && req.EndDate >= f.EndDate), ct);
        if (overlap)
            throw new DomainException("الفترة الزمنية تتداخل مع سنة مالية أخرى");

        var fy = FiscalYear.Create(trimmed, req.StartDate, req.EndDate);

        // ‎إن لم تكن هناك سنة مالية نشطة في النظام، نُفعّل هذه تلقائياً
        // ‎(تجربة سلسة: السنة الأولى تُصبح نشطة بدون خطوة إضافية).
        var hasActive = await _db.FiscalYears.AsNoTracking().AnyAsync(f => f.IsActive, ct);
        if (!hasActive) fy.Activate();

        _db.FiscalYears.Add(fy);
        await _db.SaveChangesAsync(ct);
        return fy.Id;
    }
}
