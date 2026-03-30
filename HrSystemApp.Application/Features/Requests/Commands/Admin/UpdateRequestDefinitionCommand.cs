using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Common;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using MediatR;

using Microsoft.Extensions.Logging;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

public record UpdateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
}

public class UpdateRequestDefinitionCommandHandler : IRequestHandler<UpdateRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<UpdateRequestDefinitionCommandHandler> _logger;

    public UpdateRequestDefinitionCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, ILogger<UpdateRequestDefinitionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UpdateRequestDefinitionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to update Request Definition ID {DefinitionId}.", request.Id);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<Guid>(new Error("Auth.Unauthorized", "User not authenticated."));

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
            return Result.Failure<Guid>(new Error("Employee.NotFound", "Employee profile not found."));

        // 1. Find the definition
        var definition = await _unitOfWork.RequestDefinitions.GetByIdAsync(request.Id, cancellationToken);
        if (definition == null) 
        {
            _logger.LogWarning("UpdateRequestDefinition failed: Definition ID {DefinitionId} not found.", request.Id);
            return Result.Failure<Guid>(new Error("Definition.NotFound", "Request Definition not found."));
        }

        // 2. Security: Does this user belong to the company and have admin rights?
        if (definition.CompanyId != employee.CompanyId)
        {
            _logger.LogWarning("Unauthorized update attempt for Definition {DefinitionId} by user {UserId} from different company {CompanyId}.", 
                request.Id, userId, employee.CompanyId);
            return Result.Failure<Guid>(new Error("Auth.Forbidden", "You are not authorized to update definitions for this company."));
        }

        // 2. Update
        definition.IsActive = request.IsActive;
        definition.WorkflowSteps = request.Steps.Select(s => new RequestWorkflowStep
        {
            RequiredRole = s.Role,
            SortOrder = s.SortOrder,
            RequestDefinitionId = definition.Id
        }).ToList();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully updated Request Definition ID {DefinitionId} (Type: {Type}). New step count: {StepCount}", 
            definition.Id, definition.RequestType, definition.WorkflowSteps.Count);

        return Result.Success(definition.Id);
    }
}
