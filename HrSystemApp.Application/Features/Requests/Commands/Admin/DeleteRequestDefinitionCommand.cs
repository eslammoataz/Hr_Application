using HrSystemApp.Application.Common;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HrSystemApp.Application.Features.Requests.Commands.Admin;

public record DeleteRequestDefinitionCommand(Guid Id) : IRequest<Result<Guid>>;

public class DeleteRequestDefinitionCommandHandler : IRequestHandler<DeleteRequestDefinitionCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteRequestDefinitionCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public DeleteRequestDefinitionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<DeleteRequestDefinitionCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(DeleteRequestDefinitionCommand request, CancellationToken cancellationToken)
    {

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequestDefinition, LogStage.Authorization,
                "UserNotAuthenticated", null);
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
        if (employee == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequestDefinition, LogStage.Authorization,
                "EmployeeNotFound", new { UserId = userId });
            return Result.Failure<Guid>(DomainErrors.Employee.NotFound);
        }

        var definition = await _unitOfWork.RequestDefinitions.GetByIdAsync(request.Id, cancellationToken);
        if (definition == null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequestDefinition, LogStage.Validation,
                "DefinitionNotFound", new { DefinitionId = request.Id });
            return Result.Failure<Guid>(DomainErrors.Requests.DefinitionNotFound);
        }

        if (definition.CompanyId != employee.CompanyId)
        {
            _logger.LogDecision(_loggingOptions, LogAction.Workflow.DeleteRequestDefinition, LogStage.Authorization,
                "UnauthorizedCrossCompany", new { DefinitionId = request.Id, UserId = userId });
            return Result.Failure<Guid>(DomainErrors.Auth.Unauthorized);
        }

        await _unitOfWork.RequestDefinitions.DeleteAsync(definition, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(definition.Id);
    }
}
