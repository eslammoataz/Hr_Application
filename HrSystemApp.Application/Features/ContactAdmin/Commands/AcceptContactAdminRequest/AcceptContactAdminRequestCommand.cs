using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.AcceptContactAdminRequest;

public record AcceptContactAdminRequestCommand(Guid Id) : IRequest<Result>;
