using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Interfaces.Services;

public interface IDataScopeService
{
    Result<Guid?> ResolveEmployeeCompanyScope(Guid? requestedCompanyId);
}
