using HrSystemApp.Application.DTOs.Departments;
using HrSystemApp.Application.DTOs.Teams;
using HrSystemApp.Application.DTOs.Units;
using HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;
using HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;
using HrSystemApp.Application.Features.Units.Commands.UpdateUnit;
using HrSystemApp.Domain.Models;
using Mapster;

namespace HrSystemApp.Application.Mappings;

public class HierarchyMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // ── Department ──────────────────────────────────────────────────
        config.NewConfig<Department, DepartmentResponse>()
            .Map(dest => dest.VicePresidentName, src => src.VicePresident != null ? src.VicePresident.FullName : null)
            .Map(dest => dest.ManagerName, src => src.Manager != null ? src.Manager.FullName : null);

        config.NewConfig<Department, DepartmentWithUnitsResponse>()
            .Map(dest => dest.VicePresidentName, src => src.VicePresident != null ? src.VicePresident.FullName : null)
            .Map(dest => dest.ManagerName, src => src.Manager != null ? src.Manager.FullName : null)
            .Map(dest => dest.Units, src => src.Units);

        config.NewConfig<Unit, UnitSummary>();

        // Partial update: ignore nulls
        config.NewConfig<UpdateDepartmentCommand, Department>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id);

        // ── Unit ─────────────────────────────────────────────────────────
        config.NewConfig<Unit, UnitResponse>()
            .Map(dest => dest.DepartmentName, src => src.Department != null ? src.Department.Name : "")
            .Map(dest => dest.UnitLeaderName, src => src.UnitLeader != null ? src.UnitLeader.FullName : null);

        config.NewConfig<UpdateUnitCommand, Unit>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id);

        // ── Team ─────────────────────────────────────────────────────────
        config.NewConfig<Team, TeamResponse>()
            .Map(dest => dest.UnitName, src => src.Unit != null ? src.Unit.Name : "")
            .Map(dest => dest.TeamLeaderName, src => src.TeamLeader != null ? src.TeamLeader.FullName : null);

        config.NewConfig<Team, TeamWithMembersResponse>()
            .Map(dest => dest.UnitName, src => src.Unit != null ? src.Unit.Name : "")
            .Map(dest => dest.TeamLeaderName, src => src.TeamLeader != null ? src.TeamLeader.FullName : null)
            .Map(dest => dest.Members, src => src.Members);

        config.NewConfig<Employee, MemberSummary>();

        config.NewConfig<UpdateTeamCommand, Team>()
            .IgnoreNullValues(true)
            .Ignore(dest => dest.Id);
    }
}
