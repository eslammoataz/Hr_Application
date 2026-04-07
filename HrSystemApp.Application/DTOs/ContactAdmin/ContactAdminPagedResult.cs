using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.DTOs.ContactAdmin;

public class ContactAdminPagedResult : PagedResult<ContactAdminRequestDto>
{
    public int TotalPending { get; init; }
    public int TotalAccepted { get; init; }
    public int TotalRejected { get; init; }
}
