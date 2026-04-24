using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Queries.GetUserRequests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Requests;

/// <summary>
/// Tests for GetUserRequestsQueryHandler covering:
/// - IQueryable-based pagination (H-1 fix)
/// - Auth/employee guard checks
/// - Correct mapping to RequestDto
/// </summary>
public class GetUserRequestsQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRequestRepository> _requestRepo;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<ICurrentUserService> _currentUserService;

    public GetUserRequestsQueryHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _requestRepo = new Mock<IRequestRepository>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _currentUserService = new Mock<ICurrentUserService>();

        _unitOfWork.SetupGet(x => x.Requests).Returns(_requestRepo.Object);
        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private GetUserRequestsQueryHandler CreateHandler()
        => new(_unitOfWork.Object, _currentUserService.Object);

    // ─── Auth guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsUnauthorized()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns(string.Empty);

        var result = await CreateHandler().Handle(new GetUserRequestsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    // ─── Employee guard ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenEmployeeNotFound_ReturnsEmployeeNotFound()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns("user-1");

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreateHandler().Handle(new GetUserRequestsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Employee.NotFound);
    }

    // ─── Success path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCalled_ReturnsPagedRequestDtos()
    {
        var employeeId = Guid.NewGuid();
        var userId = "user-1";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = Guid.NewGuid() });

        var requestId = Guid.NewGuid();
        var requests = new List<Request>
        {
            new()
            {
                Id = requestId, EmployeeId = employeeId,
                RequestType = RequestType.Leave, Status = RequestStatus.Submitted,
                Data = "{}", PlannedStepsJson = "[]",
                Employee = new Employee { FullName = "User", EmployeeCode = "E001" }
            }
        }.AsQueryable();

        _requestRepo
            .Setup(x => x.QueryByEmployeeId(employeeId))
            .Returns(requests);

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.ToList());

        var result = await CreateHandler().Handle(
            new GetUserRequestsQuery { PageNumber = 1, PageSize = 10 }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Id.Should().Be(requestId);
        result.Value.Items[0].Type.Should().Be(RequestType.Leave);
    }

    [Fact]
    public async Task Handle_UsesQueryableRepository_NotFindAsync()
    {
        var employeeId = Guid.NewGuid();
        var userId = "user-1";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = Guid.NewGuid() });

        _requestRepo
            .Setup(x => x.QueryByEmployeeId(employeeId))
            .Returns(new List<Request>().AsQueryable());

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Request>());

        await CreateHandler().Handle(new GetUserRequestsQuery(), CancellationToken.None);

        _requestRepo.Verify(x => x.QueryByEmployeeId(employeeId), Times.Once);
        _requestRepo.Verify(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepo.Verify(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoRequests_ReturnsEmptyPagedResult()
    {
        var employeeId = Guid.NewGuid();
        var userId = "user-1";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId, CompanyId = Guid.NewGuid() });

        _requestRepo
            .Setup(x => x.QueryByEmployeeId(employeeId))
            .Returns(new List<Request>().AsQueryable());

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Request>());

        var result = await CreateHandler().Handle(new GetUserRequestsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}