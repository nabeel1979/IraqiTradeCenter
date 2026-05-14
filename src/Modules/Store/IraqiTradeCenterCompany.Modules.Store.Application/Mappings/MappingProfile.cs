using AutoMapper;
using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SalesInvoice, SalesInvoiceDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RemainingAmount, o => o.MapFrom(s => s.RemainingAmount));
        CreateMap<SalesInvoiceLine, SalesInvoiceLineDto>();

        CreateMap<SalesRep, SalesRepDto>()
            .ForMember(d => d.CommissionType, o => o.MapFrom(s => s.CommissionType.ToString()));

        CreateMap<IncomingOrder, IncomingOrderDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<IncomingOrderItem, IncomingOrderItemDto>();
    }
}
