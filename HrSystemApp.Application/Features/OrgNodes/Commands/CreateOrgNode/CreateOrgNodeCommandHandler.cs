using System.Diagnostics;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HrSystemApp.Application.Common.Logging;

namespace HrSystemApp.Application.Features.OrgNodes.Commands.CreateOrgNode;

public class CreateOrgNodeCommandHandler : IRequestHandler<CreateOrgNodeCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateOrgNodeCommandHandler> _logger;
    private readonly LoggingOptions _loggingOptions;

    public CreateOrgNodeCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ILogger<CreateOrgNodeCommandHandler> logger,
        IOptions<LoggingOptions> loggingOptions)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _logger = logger;
        _loggingOptions = loggingOptions.Value;
    }

    public async Task<Result<Guid>> Handle(CreateOrgNodeCommand request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogActionStart(_loggingOptions, LogAction.OrgNode.CreateOrgNode);

        if (request.ParentId.HasValue)
        {
            var parent = await _unitOfWork.OrgNodes.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent == null)
            {
                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.CreateOrgNode, LogStage.Processing,
                    "ParentNotFound", new { ParentId = request.ParentId });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.OrgNode.NotFound);
            }

            if (parent.CompanyId != request.CompanyId)
            {
                _logger.LogDecision(_loggingOptions, LogAction.OrgNode.CreateOrgNode, LogStage.Processing,
                    "ParentCompanyMismatch", new { ParentId = request.ParentId, ParentCompanyId = parent.CompanyId });
                sw.Stop();
                return Result.Failure<Guid>(DomainErrors.General.ArgumentError);
            }
        }

        var node = new OrgNode
        {
            Name = request.Name,
            ParentId = request.ParentId,
            Type = request.Type?.Trim().ToLower(),
            CompanyId = request.CompanyId
        };

        await _unitOfWork.OrgNodes.AddAsync(node, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogActionSuccess(_loggingOptions, LogAction.OrgNode.CreateOrgNode, sw.ElapsedMilliseconds);

        return Result.Success(node.Id);
    }
}