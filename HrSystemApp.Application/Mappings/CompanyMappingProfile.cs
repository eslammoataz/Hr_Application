using AutoMapper;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Mappings;

public class CompanyMappingProfile : Profile
{
    public CompanyMappingProfile()
    {
        CreateMap<CreateCompanyCommand, Company>()
            .AfterMap((_, dest) => dest.Status = CompanyStatus.Active);

        CreateMap<Company, CompanyResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        CreateMap<CompanyLocation, CompanyLocationResponse>();
    }
}
