using MediatR;
using HrSystemApp.Application.Common;

namespace HrSystemApp.Application.Features.Companies.Commands.DeleteCompanyLocation;

public record DeleteCompanyLocationCommand(Guid LocationId) : IRequest<Result<Guid>>;
