using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Requests;
using HrSystemApp.Application.Errors;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Interfaces;
using MediatR;
using System.Text.Json;

namespace HrSystemApp.Application.Features.Requests.Queries.GetRequestDefinitions;

public record GetRequestDefinitionsQuery(Guid? CompanyId = null, RequestType? Type = null) : IRequest<Result<List<RequestDefinitionDto>>>;

public record RequestDefinitionDto
{
    public Guid Id { get; set; }
    public RequestType RequestType { get; set; }
    public bool IsActive { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
    public object? Schema { get; set; }
}

public class GetRequestDefinitionsQueryHandler : IRequestHandler<GetRequestDefinitionsQuery, Result<List<RequestDefinitionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRequestSchemaValidator _validator;

    public GetRequestDefinitionsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService, IRequestSchemaValidator validator)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _validator = validator;
    }

    public async Task<Result<List<RequestDefinitionDto>>> Handle(GetRequestDefinitionsQuery request, CancellationToken cancellationToken)
    {
        Guid targetCompanyId;

        // 1. Resolve Company ID (SuperAdmin can specify, CompanyAdmin uses their own)
        if (_currentUserService.Role == nameof(UserRole.SuperAdmin))
        {
            if (!request.CompanyId.HasValue || request.CompanyId.Value == Guid.Empty)
                return Result.Failure<List<RequestDefinitionDto>>(DomainErrors.General.ArgumentError);

            targetCompanyId = request.CompanyId.Value;
        }
        else
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
                return Result.Failure<List<RequestDefinitionDto>>(DomainErrors.Auth.Unauthorized);

            var employee = await _unitOfWork.Employees.GetByUserIdAsync(userId, cancellationToken);
            if (employee == null)
                return Result.Failure<List<RequestDefinitionDto>>(DomainErrors.Employee.NotFound);

            targetCompanyId = employee.CompanyId;
        }

        // 2. Fetch Definitions
        var definitions = await _unitOfWork.RequestDefinitions.GetByCompanyAsync(targetCompanyId, request.Type, cancellationToken);

        // 3. Map to DTO
        var dtos = definitions.Select(d => new RequestDefinitionDto
        {
            Id = d.Id,
            RequestType = d.RequestType,
            IsActive = d.IsActive,
            Steps = d.WorkflowSteps.Select(s => new WorkflowStepDto
            {
                StepType = s.StepType,
                StepTypeName = s.StepType.ToString(),
                OrgNodeId = s.OrgNodeId,
                BypassHierarchyCheck = s.BypassHierarchyCheck,
                DirectEmployeeId = s.DirectEmployeeId,
                StartFromLevel = s.StartFromLevel,
                LevelsUp = s.LevelsUp,
                SortOrder = s.SortOrder
            }).ToList(),
            Schema = string.Empty
            }).ToList();

        return Result.Success(dtos);
    }
}
