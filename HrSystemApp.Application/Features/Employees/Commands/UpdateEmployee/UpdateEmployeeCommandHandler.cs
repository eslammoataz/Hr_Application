using System.Diagnostics;
using Mapster;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

public class UpdateEmployeeCommandHandler : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateEmployeeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public UpdateEmployeeCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<UpdateEmployeeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<EmployeeResponse>> Handle(UpdateEmployeeCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.OrgNode.UpdateEmployee);

        var employee = await _unitOfWork.Employees.GetWithDetailsAsync(request.Id, cancellationToken);
        if (employee is null)
        {
            _logger.LogDecision(_loggingOptions, LogAction.OrgNode.UpdateEmployee, LogStage.Processing,
                "EmployeeNotFound", new { EmployeeId = request.Id });
            sw.Stop();
            return Result.Failure<EmployeeResponse>(DomainErrors.Employee.NotFound);
        }

        request.Adapt(employee);

        await _unitOfWork.Employees.UpdateAsync(employee, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.OrgNode.UpdateEmployee, sw.ElapsedMilliseconds);

        return Result.Success(employee.Adapt<EmployeeResponse>());
    }
}