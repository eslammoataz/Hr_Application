using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.DTOs.Companies;

public class CompaniesPagedResult : PagedResult<CompanyResponse>
{
    public int TotalActive { get; init; }
    public int TotalInactive { get; init; }
    public int TotalSuspended { get; init; }
}
