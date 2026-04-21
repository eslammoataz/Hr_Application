using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Employees.Commands.ChangeEmployeeStatus;

public class ChangeEmployeeStatusCommandHandler : IRequestHandler<ChangeEmployeeStatusCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangeEmployeeStatusCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public ChangeEmployeeStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ChangeEmployeeStatusCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result> Handle(ChangeEmployeeStatusCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.OrgNode.ChangeEmployeeStatus);

        if (!Enum.IsDefined(typeof(EmploymentStatus), request.Status))
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.ChangeEmployeeStatus, LogStage.Processing,
                "InvalidStatus", new { Status = request.Status });
            sw.Stop();
            return Result.Failure(DomainErrors.Employee.InvalidEmploymentStatus);
        }

        var employee = await _unitOfWork.Employees.GetByIdAsync(request.Id, cancellationToken);
        if (employee is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.ChangeEmployeeStatus, LogStage.Processing,
                "EmployeeNotFound", new { EmployeeId = request.Id });
            sw.Stop();
            return Result.Failure(DomainErrors.Employee.NotFound);
        }

        var targetStatus = (EmploymentStatus)request.Status;
        employee.EmploymentStatus = targetStatus;

        if (employee.UserId is not null)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(employee.UserId, cancellationToken);
            if (user is not null)
            {
                user.IsActive = targetStatus is not (
                    EmploymentStatus.Inactive or
                    EmploymentStatus.Suspended or
                    EmploymentStatus.Terminated);
                await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
            }
        }

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.OrgNode.ChangeEmployeeStatus, sw.ElapsedMilliseconds);

        return Result.Success();
    }
}