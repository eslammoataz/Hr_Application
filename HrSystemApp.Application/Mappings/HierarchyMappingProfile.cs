using AutoMapper;
using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;
using HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;
using HrSystemApp.Application.Features.Units.Commands.UpdateUnit;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Mappings;

public class HierarchyMappingProfile : Profile
{
    public HierarchyMappingProfile()
    {
        // ── Department ──────────────────────────────────────────────────
        CreateMap<Department, DepartmentResponse>()
            .ForMember(d => d.VicePresidentName, o => o.MapFrom(s => s.VicePresident != null ? s.VicePresident.FullName : null))
            .ForMember(d => d.ManagerName,       o => o.MapFrom(s => s.Manager       != null ? s.Manager.FullName       : null));

        CreateMap<Department, DepartmentWithUnitsResponse>()
            .ForMember(d => d.VicePresidentName, o => o.MapFrom(s => s.VicePresident != null ? s.VicePresident.FullName : null))
            .ForMember(d => d.ManagerName,       o => o.MapFrom(s => s.Manager       != null ? s.Manager.FullName       : null))
            .ForMember(d => d.Units,             o => o.MapFrom(s => s.Units));

        CreateMap<Unit, UnitSummary>();

        // Partial update: null source members leave destination untouched
        CreateMap<UpdateDepartmentCommand, Department>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForAllMembers(o => o.Condition((_, _, srcVal) => srcVal is not null));

        // ── Unit ─────────────────────────────────────────────────────────
        CreateMap<Unit, UnitResponse>()
            .ForMember(d => d.DepartmentName, o => o.MapFrom(s => s.Department != null ? s.Department.Name    : ""))
            .ForMember(d => d.UnitLeaderName, o => o.MapFrom(s => s.UnitLeader != null ? s.UnitLeader.FullName : null));

        CreateMap<UpdateUnitCommand, Unit>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForAllMembers(o => o.Condition((_, _, srcVal) => srcVal is not null));

        // ── Team ─────────────────────────────────────────────────────────
        CreateMap<Team, TeamResponse>()
            .ForMember(d => d.UnitName,       o => o.MapFrom(s => s.Unit       != null ? s.Unit.Name          : ""))
            .ForMember(d => d.TeamLeaderName, o => o.MapFrom(s => s.TeamLeader != null ? s.TeamLeader.FullName : null));

        CreateMap<Team, TeamWithMembersResponse>()
            .ForMember(d => d.UnitName,       o => o.MapFrom(s => s.Unit       != null ? s.Unit.Name          : ""))
            .ForMember(d => d.TeamLeaderName, o => o.MapFrom(s => s.TeamLeader != null ? s.TeamLeader.FullName : null))
            .ForMember(d => d.Members,        o => o.MapFrom(s => s.Members));

        CreateMap<Employee, MemberSummary>();

        CreateMap<UpdateTeamCommand, Team>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForAllMembers(o => o.Condition((_, _, srcVal) => srcVal is not null));
    }
}
