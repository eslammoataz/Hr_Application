using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees.ProfileUpdateRequests;
using MediatR;

namespace HrSystemApp.Application.Features.ProfileUpdateRequests.Commands.CreateProfileUpdateRequest;

public record CreateProfileUpdateRequestCommand(string UserId, CreateProfileUpdateRequestDto Dto) : IRequest<Result>;
