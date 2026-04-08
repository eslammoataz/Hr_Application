using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Requests.Queries.GetMyLeaveBalances;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Requests;

public class GetMyLeaveBalancesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var sut = new GetMyLeaveBalancesQueryHandler(unitOfWork.Object, currentUser.Object);

        var result = await sut.Handle(new GetMyLeaveBalancesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task Handle_WhenEmployeeExists_ReturnsBalances()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var leaveRepo = new Mock<ILeaveBalanceRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.LeaveBalances).Returns(leaveRepo.Object);

        var employeeId = Guid.NewGuid();
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee { Id = employeeId });
        leaveRepo
            .Setup(x => x.GetByEmployeeAsync(employeeId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaveBalance>
            {
                new()
                {
                    EmployeeId = employeeId,
                    LeaveType = LeaveType.Annual,
                    Year = DateTime.UtcNow.Year,
                    TotalDays = 21,
                    UsedDays = 5
                }
            });

        var sut = new GetMyLeaveBalancesQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyLeaveBalancesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].RemainingDays.Should().Be(16);
    }

    [Fact]
    public async Task Handle_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new GetMyLeaveBalancesQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyLeaveBalancesQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }
}
