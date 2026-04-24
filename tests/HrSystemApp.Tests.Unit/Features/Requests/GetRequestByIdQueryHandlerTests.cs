using FluentAssertions;
using HrSystemApp.Application.Common.Logging;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Queries.GetRequestById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Requests;

/// <summary>
/// Tests for GetRequestByIdQueryHandler covering:
/// - C-4 fix: HR users from a different company cannot read requests from other companies
/// - Access control: requester, approver, and HR/admin can read; others are rejected
/// </summary>
public class GetRequestByIdQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRequestRepository> _requestRepo;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<ICurrentUserService> _currentUserService;
    private readonly Mock<ILogger<GetRequestByIdQueryHandler>> _logger;
    private readonly Mock<IOptions<LoggingOptions>> _loggingOptions;

    public GetRequestByIdQueryHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _requestRepo = new Mock<IRequestRepository>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _currentUserService = new Mock<ICurrentUserService>();
        _logger = new Mock<ILogger<GetRequestByIdQueryHandler>>();
        _loggingOptions = new Mock<IOptions<LoggingOptions>>();
        _loggingOptions.Setup(x => x.Value).Returns(new LoggingOptions());

        _unitOfWork.SetupGet(x => x.Requests).Returns(_requestRepo.Object);
        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private GetRequestByIdQueryHandler CreateHandler()
        => new(_unitOfWork.Object, _currentUserService.Object, _logger.Object, _loggingOptions.Object);

    private Request BuildRequest(Guid requestId, Guid employeeId, Guid companyId)
    {
        var requester = new Employee { Id = employeeId, CompanyId = companyId, FullName = "John Doe" };
        return new Request
        {
            Id = requestId,
            EmployeeId = employeeId,
            RequestType = RequestType.Leave,
            Status = RequestStatus.Submitted,
            Data = "{}",
            PlannedStepsJson = "[]",
            Employee = requester,
            ApprovalHistory = new List<RequestApprovalHistory>()
        };
    }

    // ─── Request not found ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequestNotFound_ReturnsRequestNotFound()
    {
        var requestId = Guid.NewGuid();

        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request?)null);

        _currentUserService.SetupGet(x => x.UserId).Returns("user-1");
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.Employee));

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Requests.NotFound);
    }

    // ─── Requester can always access their own request ────────────────────────

    [Fact]
    public async Task Handle_WhenCallerIsRequester_Succeeds()
    {
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = "user-employee-1";

        var request = BuildRequest(requestId, employeeId, companyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(userId);
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.Employee));

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = companyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(requestId);
    }

    // ─── Non-requester / non-approver / non-HR is rejected ───────────────────

    [Fact]
    public async Task Handle_WhenCallerIsNeitherRequesterNorApproverNorHr_ReturnsUnauthorized()
    {
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var otherEmployeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = "user-other";

        var request = BuildRequest(requestId, employeeId, companyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(userId);
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.Employee));

        // Different employee — not the requester and not in PlannedSteps approvers
        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = otherEmployeeId, CompanyId = companyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    // ─── HR company isolation (C-4 fix) ──────────────────────────────────────

    [Fact]
    public async Task Handle_WhenHrUserIsFromSameCompany_Succeeds()
    {
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var sharedCompanyId = Guid.NewGuid();
        var hrUserId = "hr-user-1";

        var request = BuildRequest(requestId, employeeId, sharedCompanyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(hrUserId);
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));

        // HR employee is from the same company
        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(hrUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = sharedCompanyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenHrUserIsFromDifferentCompany_ReturnsUnauthorized()
    {
        // C-4 regression: before the fix, isHrOrAbove was true for any HR user regardless of company.
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var requestCompanyId = Guid.NewGuid();
        var hrCompanyId = Guid.NewGuid(); // different company
        var hrUserId = "hr-user-2";

        var request = BuildRequest(requestId, employeeId, requestCompanyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(hrUserId);
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));

        // HR employee from a different company
        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(hrUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = hrCompanyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Theory]
    [InlineData(nameof(UserRole.CompanyAdmin))]
    [InlineData(nameof(UserRole.Executive))]
    [InlineData(nameof(UserRole.SuperAdmin))]
    public async Task Handle_WhenAdminUserIsFromSameCompany_Succeeds(string roleName)
    {
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var sharedCompanyId = Guid.NewGuid();
        var adminUserId = "admin-user-1";

        var request = BuildRequest(requestId, employeeId, sharedCompanyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(adminUserId);
        _currentUserService.SetupGet(x => x.Role).Returns(roleName);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = sharedCompanyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(nameof(UserRole.CompanyAdmin))]
    [InlineData(nameof(UserRole.Executive))]
    public async Task Handle_WhenAdminUserIsFromDifferentCompany_ReturnsUnauthorized(string roleName)
    {
        var requestId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var requestCompanyId = Guid.NewGuid();
        var adminCompanyId = Guid.NewGuid(); // different company
        var adminUserId = "admin-user-2";

        var request = BuildRequest(requestId, employeeId, requestCompanyId);
        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        _currentUserService.SetupGet(x => x.UserId).Returns(adminUserId);
        _currentUserService.SetupGet(x => x.Role).Returns(roleName);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = adminCompanyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    // ─── Approver can read the request ───────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCallerIsApproverInPlannedSteps_Succeeds()
    {
        var requestId = Guid.NewGuid();
        var requestEmployeeId = Guid.NewGuid();
        var approverEmployeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = "user-approver";

        // Build planned steps JSON with the approver — use the PlannedStepDto shape exactly.
        var plannedStepDto = new HrSystemApp.Application.DTOs.Requests.PlannedStepDto
        {
            NodeId = Guid.NewGuid(),
            NodeName = "Finance",
            SortOrder = 1,
            Approvers = new List<HrSystemApp.Application.DTOs.Requests.ApproverDto>
            {
                new() { EmployeeId = approverEmployeeId, EmployeeName = "Approver" }
            }
        };
        var plannedStepsJson = System.Text.Json.JsonSerializer.Serialize(
            new List<HrSystemApp.Application.DTOs.Requests.PlannedStepDto> { plannedStepDto });

        var requester = new Employee { Id = requestEmployeeId, CompanyId = companyId, FullName = "Requester" };
        var req = new Request
        {
            Id = requestId,
            EmployeeId = requestEmployeeId,
            RequestType = RequestType.Leave,
            Status = RequestStatus.InProgress,
            Data = "{}",
            PlannedStepsJson = plannedStepsJson,
            Employee = requester,
            ApprovalHistory = new List<RequestApprovalHistory>()
        };

        _requestRepo
            .Setup(x => x.GetByIdWithHistoryAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(req);

        _currentUserService.SetupGet(x => x.UserId).Returns(userId);
        _currentUserService.SetupGet(x => x.Role).Returns(nameof(UserRole.Employee));

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = approverEmployeeId, CompanyId = companyId });

        var result = await CreateHandler().Handle(
            new GetRequestByIdQuery(requestId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}