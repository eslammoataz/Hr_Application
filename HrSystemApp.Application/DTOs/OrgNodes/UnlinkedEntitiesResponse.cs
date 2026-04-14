namespace HrSystemApp.Application.DTOs.OrgNodes;

public record UnlinkedEntitiesResponse(
    List<UnlinkedDepartmentDto> Departments,
    List<UnlinkedUnitDto> Units,
    List<UnlinkedTeamDto> Teams
);

public record UnlinkedDepartmentDto(Guid Id, string Name);
public record UnlinkedUnitDto(Guid Id, string Name, string DepartmentName);
public record UnlinkedTeamDto(Guid Id, string Name, string UnitName);