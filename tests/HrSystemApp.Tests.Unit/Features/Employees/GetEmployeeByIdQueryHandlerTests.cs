using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Employees.Queries.GetEmployeeById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class GetEmployeeByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenEmployeeNotFound_ReturnsNotFound()
    {
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        employeeRepo
            .Setup(x => x.GetWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new GetEmployeeByIdQueryHandler(unitOfWork.Object);

        var result = await sut.Handle(new GetEmployeeByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenEmployeeExists_ReturnsMappedResponse()
    {
        MapsterTestConfig.EnsureInitialized();

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "John",
            Email = "john@example.com",
            PhoneNumber = "0100000000",
            EmployeeCode = "EMP-99",
            CompanyId = Guid.NewGuid()
        };

        employeeRepo
            .Setup(x => x.GetWithDetailsAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var sut = new GetEmployeeByIdQueryHandler(unitOfWork.Object);

        var result = await sut.Handle(new GetEmployeeByIdQuery(employee.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("John");
    }
}
