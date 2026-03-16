using AutoMapper;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Mappings;

public class EmployeeMappingProfile : Profile
{
    public EmployeeMappingProfile()
    {
        CreateMap<Employee, CreateEmployeeResponse>()
            .ForMember(d => d.EmployeeId,        o => o.MapFrom(s => s.Id))
            .ForMember(d => d.UserId,            o => o.MapFrom(s => s.UserId ?? ""))
            .ForMember(d => d.Role,              o => o.Ignore())
            .ForMember(d => d.TemporaryPassword, o => o.Ignore());

        CreateMap<Employee, EmployeeResponse>()
            .ForMember(d => d.DepartmentName,    o => o.MapFrom(s => s.Department != null ? s.Department.Name   : null))
            .ForMember(d => d.UnitName,          o => o.MapFrom(s => s.Unit       != null ? s.Unit.Name         : null))
            .ForMember(d => d.TeamName,          o => o.MapFrom(s => s.Team       != null ? s.Team.Name         : null))
            .ForMember(d => d.ManagerName,       o => o.MapFrom(s => s.Manager    != null ? s.Manager.FullName  : null))
            .ForMember(d => d.EmploymentStatus,  o => o.MapFrom(s => s.EmploymentStatus.ToString()))
            .ForMember(d => d.Role,              o => o.Ignore())
            .ForMember(d => d.MedicalClass,      o => o.MapFrom(s => s.MedicalClass.HasValue ? s.MedicalClass.ToString() : null));

        // Partial update: null source members leave destination untouched
        CreateMap<UpdateEmployeeCommand, Employee>()
            .ForMember(d => d.Id,           o => o.Ignore())
            .ForMember(d => d.MedicalClass, o => o.MapFrom((src, _) =>
                src.MedicalClass != null && Enum.TryParse<MedicalClass>(src.MedicalClass, out var mc)
                    ? (MedicalClass?)mc
                    : null))
            .ForAllMembers(o => o.Condition((_, _, srcVal) => srcVal is not null));
    }
}
