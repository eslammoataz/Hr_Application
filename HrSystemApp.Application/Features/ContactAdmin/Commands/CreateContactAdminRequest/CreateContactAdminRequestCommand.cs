using HrSystemApp.Application.Common;
using MediatR;

namespace HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

public record CreateContactAdminRequestCommand(
    string Name,
    string Email,
    string CompanyName,
    string PhoneNumber) : IRequest<Result>;
