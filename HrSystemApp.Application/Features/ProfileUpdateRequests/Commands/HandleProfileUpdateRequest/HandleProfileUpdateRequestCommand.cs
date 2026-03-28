using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using MediatR;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.HandleProfileUpdateRequest;

public record HandleProfileUpdateRequestCommand(Guid RequestId, string HrUserId, HandleProfileUpdateRequestDto Dto) : IRequest<Result>;
