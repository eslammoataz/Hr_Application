using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

public record DeleteRequestDefinitionCommand(Guid Id) : IRequest<Result<Guid>>;

public class DeleteRequestDefinitionCommandHandler : IRequestHandler<DeleteRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteRequestDefinitionCommandHandler> _logger;

    public DeleteRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteRequestDefinitionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(DeleteRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to delete Request Definition ID {DefinitionId}.", request.Id);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);

        // 1. Find the definition
        var definition = await _unitOfWork.RequestDefinitions.GetByIdAsync(request.Id, cancellationToken);
        if (definition == null)
        {
            _logger.LogWarning("DeleteRequestDefinition failed: Definition ID {DefinitionId} not found.", request.Id);
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionNotFound);
        }

        // 2. Security: Does this user belong to the company?
        if (definition.CompanyId != employee.CompanyId)
        {
            _logger.LogWarning(
                "Unauthorized delete attempt for Definition {DefinitionId} by user {UserId} from different company {CompanyId}.",
                request.Id, userId, employee.CompanyId);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        // 3. Delete (Soft delete handled by DbContext)
        await _unitOfWork.RequestDefinitions.DeleteAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully deleted Request Definition ID {DefinitionId} for Company {CompanyId}.",
            definition.Id, definition.CompanyId);

        return Result.Success(definition.Id);
    }
}
