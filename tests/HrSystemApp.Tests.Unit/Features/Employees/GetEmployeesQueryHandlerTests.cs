using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Features.Employees.Queries.GetEmployees;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class GetEmployeesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenScopeResolved_ReturnsScopedPagedResult()
    {
        var employeeRepo = new Mock<IEmployeeRepository>();
        var scopeService = new Mock<IDataScopeService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        var scopedCompanyId = Guid.NewGuid();
        scopeService
            .Setup(x => x.ResolveEmployeeCompanyScope(It.IsAny<Guid?>()))
            .Returns(Result.Success<Guid?>(scopedCompanyId));

        var paged = new EmployeesPagedResult
        {
            Items = new[]
            {
                new EmployeeResponse
                {
                    Id = Guid.NewGuid(),
                    FullName = "Alice HR",
                    Role = nameof(UserRole.HR),
                    EmploymentStatus = nameof(EmploymentStatus.Active)
                }
            },
            PageNumber = 1,
            PageSize = 20,
            TotalCount = 1,
            TotalActive = 1,
            TotalInactive = 0
        };

        employeeRepo
            .Setup(x => x.GetPagedForListAsync(
                scopedCompanyId,
                null,
                "alice",
                UserRole.HR,
                EmploymentStatus.Active,
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var sut = new GetEmployeesQueryHandler(unitOfWork.Object, scopeService.Object);
        var query = new GetEmployeesQuery(null, null, "alice", UserRole.HR, EmploymentStatus.Active, 1, 20);

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalActive.Should().Be(1);
        result.Value.TotalInactive.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenScopeFails_ReturnsFailureAndSkipsRepositoryCall()
    {
        var employeeRepo = new Mock<IEmployeeRepository>();
        var scopeService = new Mock<IDataScopeService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        scopeService
            .Setup(x => x.ResolveEmployeeCompanyScope(It.IsAny<Guid?>()))
            .Returns(Result.Failure<Guid?>(new Error("General.Forbidden", "forbidden")));

        var sut = new GetEmployeesQueryHandler(unitOfWork.Object, scopeService.Object);
        var result = await sut.Handle(new GetEmployeesQuery(Guid.NewGuid(), null, null, null, null, 1, 20), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("General.Forbidden");
        employeeRepo.Verify(x => x.GetPagedForListAsync(
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<UserRole?>(),
                It.IsAny<EmploymentStatus?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
