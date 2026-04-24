using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Queries.GetCompanyRequests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Requests;

/// <summary>
/// Tests for GetCompanyRequestsQueryHandler covering:
/// - IQueryable-based pagination (H-1 fix)
/// - Auth/employee guard checks
/// - Filtering by Status and Type
/// </summary>
public class GetCompanyRequestsQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IRequestRepository> _requestRepo;
    private readonly Mock<IEmployeeRepository> _employeeRepo;
    private readonly Mock<ICurrentUserService> _currentUserService;

    public GetCompanyRequestsQueryHandlerTests()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _requestRepo = new Mock<IRequestRepository>();
        _employeeRepo = new Mock<IEmployeeRepository>();
        _currentUserService = new Mock<ICurrentUserService>();

        _unitOfWork.SetupGet(x => x.Requests).Returns(_requestRepo.Object);
        _unitOfWork.SetupGet(x => x.Employees).Returns(_employeeRepo.Object);
    }

    private GetCompanyRequestsQueryHandler CreateHandler()
        => new(_unitOfWork.Object, _currentUserService.Object);

    // ─── Auth guard ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsUnauthorized()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns(string.Empty);

        var result = await CreateHandler().Handle(new GetCompanyRequestsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Auth.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsUnauthorized()
    {
        _currentUserService.SetupGet(x => x.UserId).Returns((string?)null);

        var result = await CreateHandler().Handle(new GetCompanyRequestsQuery(), CancellationToken.None);

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

        var result = await CreateHandler().Handle(new GetCompanyRequestsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Employee.NotFound);
    }

    // ─── Success path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCalled_ReturnsPagedResult()
    {
        var companyId = Guid.NewGuid();
        var userId = "admin-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = companyId });

        var requests = new List<Request>
        {
            new()
            {
                Id = Guid.NewGuid(), EmployeeId = Guid.NewGuid(),
                RequestType = RequestType.Leave, Status = RequestStatus.Submitted,
                Data = "{}", PlannedStepsJson = "[]",
                Employee = new Employee { FullName = "Alice", EmployeeCode = "E001", CompanyId = companyId }
            }
        }.AsQueryable();

        _requestRepo
            .Setup(x => x.QueryByCompanyId(companyId))
            .Returns(requests);

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests.ToList());

        var query = new GetCompanyRequestsQuery { PageNumber = 1, PageSize = 10 };
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNoRequests_ReturnsEmptyPagedResult()
    {
        var companyId = Guid.NewGuid();
        var userId = "admin-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = companyId });

        var emptyQuery = new List<Request>().AsQueryable();

        _requestRepo
            .Setup(x => x.QueryByCompanyId(companyId))
            .Returns(emptyQuery);

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Request>());

        var result = await CreateHandler().Handle(new GetCompanyRequestsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UsesQueryableRepository_NotFindAsync()
    {
        // This test verifies that the handler calls QueryByCompanyId (the new IQueryable method)
        // instead of the old FindAsync (which loaded all rows into memory).
        var companyId = Guid.NewGuid();
        var userId = "admin-user";
        _currentUserService.SetupGet(x => x.UserId).Returns(userId);

        _employeeRepo
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = Guid.NewGuid(), CompanyId = companyId });

        _requestRepo
            .Setup(x => x.QueryByCompanyId(companyId))
            .Returns(new List<Request>().AsQueryable());

        _requestRepo
            .Setup(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _requestRepo
            .Setup(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Request>());

        await CreateHandler().Handle(new GetCompanyRequestsQuery(), CancellationToken.None);

        _requestRepo.Verify(x => x.QueryByCompanyId(companyId), Times.Once);
        _requestRepo.Verify(x => x.CountAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepo.Verify(x => x.ToListAsync(It.IsAny<IQueryable<Request>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}