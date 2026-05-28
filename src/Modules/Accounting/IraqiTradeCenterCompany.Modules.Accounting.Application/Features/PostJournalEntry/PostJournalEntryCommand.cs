using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;

public record PostJournalEntryCommand(
    DateTime EntryDate,
    string Description,
    List<JournalLineRequest> Lines,
    JournalEntryType EntryType = JournalEntryType.Normal,
    string Currency = "IQD",
    bool PostImmediately = true,
    int? VoucherTypeId = null,
    /// <summary>
    /// رقم يدوي اختياري يُسجِّله المستخدم (رقم شيك، إيصال خارجي، …) — يُحفَظ
    /// كما هو ويظهر في القائمة، كما يدخل في فلتر البحث.
    /// </summary>
    string? ManualNumber = null
) : IRequest<Result<int>>;

public record JournalLineRequest(int AccountId, bool IsDebit, decimal Amount, string? Description);
