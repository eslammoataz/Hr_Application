using AutoMapper;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Mappings;

public class CompanyMappingProfile : Profile
{
    public CompanyMappingProfile()
    {
        CreateMap<Company, CompanyResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<CompanyLocation, CompanyLocationResponse>();
    }
}
