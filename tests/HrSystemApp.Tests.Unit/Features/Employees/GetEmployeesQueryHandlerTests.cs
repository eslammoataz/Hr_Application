using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.Employees.Queries.GetEmployees;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class GetEmployeesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenEmployeesExist_ReturnsPagedResultWithMappedFields()
    {
        MapsterTestConfig.EnsureInitialized();

        var employeeRepo = new Mock<IEmployeeRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.Users).Returns(userRepo.Object);

        var userId = Guid.NewGuid().ToString();

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            EmployeeCode = "EMP-001",
            FullName = "Alice HR",
            Email = "alice@example.com",
            PhoneNumber = "0123456789",
            Department = new Department { Name = "People" },
            Unit = new HrSystemApp.Domain.Models.Unit { Name = "Operations" },
            Team = new Team { Name = "Core" },
            Manager = new Employee { FullName = "Manager One" },
            UserId = userId
        };

        userRepo
            .Setup(x => x.GetPrimaryRolesByUserIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.Contains(userId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { { userId, "HR Admin" } });

        var paged = PagedResult<Employee>.Create(new List<Employee> { employee }, 1, 20, 1);

        employeeRepo
            .Setup(x => x.GetPagedAsync(null, null, "alice", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var sut = new GetEmployeesQueryHandler(unitOfWork.Object);
        var query = new GetEmployeesQuery(null, null, "alice", 1, 20);

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].FullName.Should().Be("Alice HR");
        result.Value.Items[0].DepartmentName.Should().Be("People");
        result.Value.Items[0].UnitName.Should().Be("Operations");
        result.Value.Items[0].TeamName.Should().Be("Core");
        result.Value.Items[0].ManagerName.Should().Be("Manager One");
        result.Value.Items[0].Role.Should().Be("HR Admin");
    }

    [Fact]
    public async Task Handle_WhenNoEmployees_ReturnsEmptyItemsWithPaging()
    {
        MapsterTestConfig.EnsureInitialized();

        var employeeRepo = new Mock<IEmployeeRepository>();
        var userRepo = new Mock<IUserRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.Users).Returns(userRepo.Object);

        userRepo
            .Setup(x => x.GetPrimaryRolesByUserIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        employeeRepo
            .Setup(x => x.GetPagedAsync(Guid.Empty, Guid.Empty, null, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<Employee>.Create(Array.Empty<Employee>(), 2, 10, 0));

        var sut = new GetEmployeesQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetEmployeesQuery(Guid.Empty, Guid.Empty, null, 2, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.PageNumber.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
    }
}
