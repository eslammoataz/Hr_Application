using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.RejectContactAdminRequest;

public record RejectContactAdminRequestCommand(Guid Id) : IRequest<Result>;
