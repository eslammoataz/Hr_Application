using System.Text.Json;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Requests.Strategies;

public interface IRequestBusinessStrategy
{
    RequestType Type { get; }

    /// <summary>
    /// Validates the business rules for this request type (e.g. check leave balance).
    /// </summary>
    Task<Result> ValidateBusinessRulesAsync(Guid employeeId, JsonElement data, CancellationToken ct);

    /// <summary>
    /// Executes final actions upon full approval (e.g. deduct leave balance, update asset status).
    /// </summary>
    Task OnFinalApprovalAsync(Request request, CancellationToken ct);
}
