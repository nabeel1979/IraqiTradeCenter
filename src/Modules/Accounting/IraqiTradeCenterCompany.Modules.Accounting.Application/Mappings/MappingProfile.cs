using AutoMapper;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Account, AccountDto>();
        CreateMap<JournalEntry, JournalEntryDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<JournalEntryLine, JournalLineDto>();
    }
}
