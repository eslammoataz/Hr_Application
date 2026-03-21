using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class CompanyMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateCompanyCommand, Company>()
            .AfterMapping((_, dest) => dest.Status = CompanyStatus.Active);

        config.NewConfig<Company, CompanyResponse>()
            .Map(dest => dest.Status, src => src.Status.ToString());

        config.NewConfig<CompanyLocation, CompanyLocationResponse>();
    }
}
