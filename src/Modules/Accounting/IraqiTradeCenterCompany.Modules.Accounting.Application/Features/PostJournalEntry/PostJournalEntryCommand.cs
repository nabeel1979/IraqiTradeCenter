using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;

public record PostJournalEntryCommand(
    DateTime EntryDate,
    string Description,
    List<JournalLineRequest> Lines
) : IRequest<Result<int>>;

public record JournalLineRequest(int AccountId, bool IsDebit, decimal Amount, string? Description);
