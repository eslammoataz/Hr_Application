using FluentAssertions;
using HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class UpdateEmployeeCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEmployeeExists_UpdatesAndSavesOnce()
    {
        MapsterTestConfig.EnsureInitialized();

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Before Name",
            PhoneNumber = "010",
            Email = "before@example.com",
            CompanyId = Guid.NewGuid(),
            EmployeeCode = "EMP-002"
        };

        employeeRepo
            .Setup(x => x.GetWithDetailsAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        employeeRepo
            .Setup(x => x.UpdateAsync(employee, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateEmployeeCommand(
            employee.Id,
            "After Name",
            "011",
            "Cairo",
            null,
            null,
            null,
            null,
            null,
            null);

        var sut = new UpdateEmployeeCommandHandler(unitOfWork.Object);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("After Name");
        employeeRepo.Verify(x => x.UpdateAsync(employee, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
