using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Queries.GetMyApprovalActions;
using HrSystemApp.Application.Features.Requests.Queries.GetPendingApprovals;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Requests;

/// <summary>
/// Tests for GetPendingApprovalsQueryHandler and GetMyApprovalActionsQueryHandler covering:
/// - IQueryable-based pagination (H-1 fix)
/// - Auth/employee guard checks
/// </summary>
public class GetPendingApprovalsQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRequestRepository> _requestRepo;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<ICurrentUserService> _currentUserService;

    public GetPendingApprovalsQueryHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _requestRepo = new Mock<IRequestRepository>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _currentUserService = new Mock<ICurrentUserService>();

        _unitOfWork.SetupGet(x => x.Requests).Returns(_requestRepo.Object);
        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private GetPendingApprovalsQueryHandler CreatePendingHandler()
        => new(_unitOfWork.Object, _currentUserService.Object);

    private GetMyApprovalActionsQueryHandler CreateActionsHandler()
        => new(_unitOfWork.Object, _currentUserService.Object);

    // ─── GetPendingApprovals auth guard ───────────────────────────────────────

    [Fact]
    public async Task GetPendingApprovals_WhenUserIdIsEmpty_ReturnsUnauthorized()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns(string.Empty);

        var result = await CreatePendingHandler().Handle(new GetPendingApprovalsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task GetPendingApprovals_WhenEmployeeNotFound_ReturnsEmployeeNotFound()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns("user-1");

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreatePendingHandler().Handle(new GetPendingApprovalsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Employee.NotFound);
    }

    // ─── GetPendingApprovals success path ─────────────────────────────────────

    [Fact]
    public async Task GetPendingApprovals_WhenCalled_ReturnsPagedResult()
    {
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = "approver-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = companyId });

        var requests = new List<Request>
        {
            new()
            {
                Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid(),
                RequestType = RequestType.Leave, Status = RequestStatus.InProgress,
                Data = "{}", PlannedStepsJson = "[]",
                Employee = new Employee { FullName = "Requester", EmployeeCode = "E001", CompanyId = companyId }
            }
        }.AsQueryable();

        _requestRepo
            .Setup(x => x.QueryPendingApprovals(employeeId))
            .Returns(requests);

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.ToList());

        var result = await CreatePendingHandler().Handle(
            new GetPendingApprovalsQuery { PageNumber = 1, PageSize = 10 }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPendingApprovals_UsesQueryPendingApprovalsMethod()
    {
        var employeeId = Guid.NewGuid();
        var userId = "approver-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = Guid.NewGuid() });

        _requestRepo
            .Setup(x => x.QueryPendingApprovals(employeeId))
            .Returns(new List<Request>().AsQueryable());

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Request>());

        await CreatePendingHandler().Handle(new GetPendingApprovalsQuery(), CancellationToken.None);

        _requestRepo.Verify(x => x.QueryPendingApprovals(employeeId), Times.Once);
    }

    // ─── GetMyApprovalActions auth guard ─────────────────────────────────────

    [Fact]
    public async Task GetMyApprovalActions_WhenUserIdIsEmpty_ReturnsUnauthorized()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns(string.Empty);

        var result = await CreateActionsHandler().Handle(new GetMyApprovalActionsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task GetMyApprovalActions_WhenEmployeeNotFound_ReturnsEmployeeNotFound()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns("user-1");

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreateActionsHandler().Handle(new GetMyApprovalActionsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Employee.NotFound);
    }

    // ─── GetMyApprovalActions success path ───────────────────────────────────

    [Fact]
    public async Task GetMyApprovalActions_WhenCalled_ReturnsPagedResult()
    {
        var employeeId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = "approver-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = companyId });

        var requestId = Guid.NewGuid();
        var request = new Request
        {
            Id = requestId, EmployeeId = Guid.NewGuid(),
            RequestType = RequestType.Leave, Status = RequestStatus.Approved,
            Data = "{}", PlannedStepsJson = "[]",
            Employee = new Employee { FullName = "Requester", EmployeeCode = "E002", CompanyId = companyId }
        };

        var historyEntries = new List<RequestApprovalHistory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                ApproverId = employeeId,
                Status = RequestStatus.Approved,
                Request = request,
                Approver = new Employee { Id = employeeId, FullName = "Approver" }
            }
        }.AsQueryable();

        _requestRepo
            .Setup(x => x.QueryApprovalActions(employeeId))
            .Returns(historyEntries);

        _requestRepo
            .Setup(x => x.CountHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _requestRepo
            .Setup(x => x.ToListHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(historyEntries.ToList());

        var result = await CreateActionsHandler().Handle(
            new GetMyApprovalActionsQuery { PageNumber = 1, PageSize = 10 }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].RequestId.Should().Be(requestId);
    }

    [Fact]
    public async Task GetMyApprovalActions_UsesQueryApprovalActionsMethod()
    {
        var employeeId = Guid.NewGuid();
        var userId = "approver-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = Guid.NewGuid() });

        _requestRepo
            .Setup(x => x.QueryApprovalActions(employeeId))
            .Returns(new List<RequestApprovalHistory>().AsQueryable());

        _requestRepo
            .Setup(x => x.CountHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RequestApprovalHistory>());

        await CreateActionsHandler().Handle(new GetMyApprovalActionsQuery(), CancellationToken.None);

        _requestRepo.Verify(x => x.QueryApprovalActions(employeeId), Times.Once);
        _requestRepo.Verify(x => x.CountHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepo.Verify(x => x.ToListHistoryAsync(It.IsAny<IQueryable<RequestApprovalHistory>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}