namespace HrSystemApp.Application.DTOs.Teams;

public record CreateTeamRequest(
    Guid UnitId,
    string Name,
    string? Description,
    Guid? TeamLeaderId);

public record UpdateTeamRequest(
    string? Name,
    string? Description,
    Guid? TeamLeaderId);

public record TeamResponse(
    Guid Id,
    Guid UnitId,
    string UnitName,
    string Name,
    string? Description,
    Guid? TeamLeaderId,
    string? TeamLeaderName,
    DateTime CreatedAt);

public record TeamWithMembersResponse(
    Guid Id,
    Guid UnitId,
    string UnitName,
    string Name,
    string? Description,
    Guid? TeamLeaderId,
    string? TeamLeaderName,
    IReadOnlyList<MemberSummary> Members,
    DateTime CreatedAt);

public record MemberSummary(Guid Id, string FullName, string Email);
