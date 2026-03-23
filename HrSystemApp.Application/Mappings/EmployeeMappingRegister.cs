using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class EmployeeMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Employee, CreateEmployeeResponse>()
            .Map(dest => dest.EmployeeId, src => src.Id)
            .Map(dest => dest.UserId, src => src.UserId ?? "")
            .Ignore(dest => dest.Role)
            .Ignore(dest => dest.TemporaryPassword);

        config.NewConfig<Employee, EmployeeResponse>()
            .Map(dest => dest.DepartmentName, src => src.Department != null ? src.Department.Name : null)
            .Map(dest => dest.UnitName, src => src.Unit != null ? src.Unit.Name : null)
            .Map(dest => dest.TeamName, src => src.Team != null ? src.Team.Name : null)
            .Map(dest => dest.ManagerName, src => src.Manager != null ? src.Manager.FullName : null)
            .Map(dest => dest.EmploymentStatus, src => src.EmploymentStatus.ToString())
            .Ignore(dest => dest.Role)
            .Map(dest => dest.MedicalClass, src => src.MedicalClass.HasValue ? src.MedicalClass.ToString() : null);

        config.NewConfig<UpdateEmployeeCommand, Employee>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id)
            .Map(dest => dest.MedicalClass, src => ParseMedicalClass(src.MedicalClass));
    }

    private static MedicalClass? ParseMedicalClass(string? medicalClass)
    {
        return medicalClass != null && Enum.TryParse<MedicalClass>(medicalClass, out var mc)
            ? (MedicalClass?)mc
            : null;
    }
}
