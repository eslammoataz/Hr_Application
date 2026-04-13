using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;
using DomainUnit = HrSystemApp.Domain.Models.Unit;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class CreateEmployeeCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlacementIsValid_CreatesEmployeeWithoutManager()
    {
        var companyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var unitOfWork = new Mock<IUnitOfWork>();
        var usersRepo = new Mock<IUserRepository>();
        var companiesRepo = new Mock<ICompanyRepository>();
        var departmentsRepo = new Mock<IDepartmentRepository>();
        var unitsRepo = new Mock<IUnitRepository>();
        var teamsRepo = new Mock<ITeamRepository>();
        var employeesRepo = new Mock<IEmployeeRepository>();
        var leaveBalancesRepo = new Mock<ILeaveBalanceRepository>();
        var placementService = new Mock<IEmployeePlacementService>();

        unitOfWork.SetupGet(x => x.Users).Returns(usersRepo.Object);
        unitOfWork.SetupGet(x => x.Companies).Returns(companiesRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentsRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitsRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamsRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeesRepo.Object);
        unitOfWork.SetupGet(x => x.LeaveBalances).Returns(leaveBalancesRepo.Object);

        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, departmentId, unitId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<(Guid?, Guid?, Guid?)>((departmentId, unitId, teamId)));
        placementService
            .Setup(x => x.AssignLeadershipIfNeededAsync(It.IsAny<Employee>(), UserRole.Employee, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        usersRepo
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApplicationUser>());
        companiesRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company
            {
                Id = companyId,
                CompanyName = "Acme",
                YearlyVacationDays = 21
            });
        usersRepo
            .Setup(x => x.CreateUserAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        employeesRepo
            .Setup(x => x.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee employee, CancellationToken _) => employee);
        leaveBalancesRepo
            .Setup(x => x.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance balance, CancellationToken _) => balance);

        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Employee? createdEmployee = null;
        employeesRepo
            .Setup(x => x.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .Callback<Employee, CancellationToken>((employee, _) => createdEmployee = employee)
            .ReturnsAsync((Employee employee, CancellationToken _) => employee);

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand(
            "John Doe",
            "john@acme.com",
            "01234567890",
            companyId,
            UserRole.Employee,
            departmentId,
            unitId,
            teamId);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        createdEmployee.Should().NotBeNull();
        createdEmployee!.DepartmentId.Should().Be(departmentId);
        createdEmployee.UnitId.Should().Be(unitId);
        createdEmployee.TeamId.Should().Be(teamId);
        createdEmployee.ManagerId.Should().BeNull();
        unitOfWork.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTeamMissing_ReturnsTeamNotFound()
    {
        var companyId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var teamsRepo = Mock.Get(unitOfWork.Object.Teams);
        var placementService = new Mock<IEmployeePlacementService>();

        teamsRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, It.IsAny<Guid?>(), It.IsAny<Guid?>(), teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Team.NotFound));

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand("John", "john@acme.com", "01234567890", companyId, UserRole.Employee, null, null, teamId);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenUnitMissing_ReturnsUnitNotFound()
    {
        var companyId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var unitsRepo = Mock.Get(unitOfWork.Object.Units);
        var placementService = new Mock<IEmployeePlacementService>();

        unitsRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUnit?)null);
        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, It.IsAny<Guid?>(), unitId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Unit.NotFound));

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand("John", "john@acme.com", "01234567890", companyId, UserRole.Employee, null, unitId, null);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenDepartmentMissing_ReturnsDepartmentNotFound()
    {
        var companyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var departmentsRepo = Mock.Get(unitOfWork.Object.Departments);
        var placementService = new Mock<IEmployeePlacementService>();

        departmentsRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);
        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, departmentId, It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.Department.NotFound));

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand("John", "john@acme.com", "01234567890", companyId, UserRole.Employee, departmentId, null, null);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenTeamUnitMismatch_ReturnsInvalidOperation()
    {
        var companyId = Guid.NewGuid();
        var requestedUnitId = Guid.NewGuid();
        var actualUnitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var teamsRepo = Mock.Get(unitOfWork.Object.Teams);
        var placementService = new Mock<IEmployeePlacementService>();

        teamsRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = teamId, UnitId = actualUnitId, Name = "Team A" });
        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, It.IsAny<Guid?>(), requestedUnitId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation));

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand("John", "john@acme.com", "01234567890", companyId, UserRole.Employee, null, requestedUnitId, teamId);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.InvalidOperation.Code);
    }

    [Fact]
    public async Task Handle_WhenDepartmentCompanyMismatch_ReturnsInvalidOperation()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var departmentsRepo = Mock.Get(unitOfWork.Object.Departments);
        var placementService = new Mock<IEmployeePlacementService>();

        departmentsRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, CompanyId = otherCompanyId, Name = "Dept A" });
        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, departmentId, It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<(Guid?, Guid?, Guid?)>(DomainErrors.General.InvalidOperation));

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand("John", "john@acme.com", "01234567890", companyId, UserRole.Employee, departmentId, null, null);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.InvalidOperation.Code);
    }

    [Fact]
    public async Task Handle_WhenCreatingTeamLeader_UpdatesTeamAndDemotesOldLeader()
    {
        var companyId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var oldLeaderEmployeeId = Guid.NewGuid();

        var unitOfWork = BuildBaseCreateEmployeeUnitOfWork(companyId);
        var teamsRepo = Mock.Get(unitOfWork.Object.Teams);
        var unitsRepo = Mock.Get(unitOfWork.Object.Units);
        var departmentsRepo = Mock.Get(unitOfWork.Object.Departments);
        var usersRepo = Mock.Get(unitOfWork.Object.Users);
        var employeesRepo = Mock.Get(unitOfWork.Object.Employees);
        var placementService = new Mock<IEmployeePlacementService>();

        var team = new Team
        {
            Id = teamId,
            UnitId = unitId,
            Name = "Team A",
            TeamLeaderId = oldLeaderEmployeeId
        };

        teamsRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        unitsRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomainUnit { Id = unitId, DepartmentId = departmentId, Name = "Unit A" });
        departmentsRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, CompanyId = companyId, Name = "Dept A" });

        var oldLeaderUser = new ApplicationUser { Id = "old-user-id", EmployeeId = oldLeaderEmployeeId };
        var usersInStore = new List<ApplicationUser> { oldLeaderUser };
        usersRepo
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<ApplicationUser, bool>> expr, CancellationToken _) =>
                usersInStore.Where(expr.Compile()).ToList());

        usersRepo
            .Setup(x => x.RemoveFromRoleAsync(oldLeaderUser, UserRole.TeamLeader.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        usersRepo
            .Setup(x => x.AddToRoleAsync(oldLeaderUser, UserRole.Employee.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        placementService
            .Setup(x => x.ResolvePlacementAsync(companyId, departmentId, unitId, teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<(Guid?, Guid?, Guid?)>((departmentId, unitId, teamId)));

        placementService
            .Setup(x => x.AssignLeadershipIfNeededAsync(It.IsAny<Employee>(), UserRole.TeamLeader, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new CreateEmployeeCommandHandler(unitOfWork.Object, placementService.Object);
        var command = new CreateEmployeeCommand(
            "Jane Doe",
            "jane@acme.com",
            "01234567890",
            companyId,
            UserRole.TeamLeader,
            departmentId,
            unitId,
            teamId);

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        placementService.Verify(
            x => x.AssignLeadershipIfNeededAsync(It.IsAny<Employee>(), UserRole.TeamLeader, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IUnitOfWork> BuildBaseCreateEmployeeUnitOfWork(Guid companyId)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var usersRepo = new Mock<IUserRepository>();
        var companiesRepo = new Mock<ICompanyRepository>();
        var departmentsRepo = new Mock<IDepartmentRepository>();
        var unitsRepo = new Mock<IUnitRepository>();
        var teamsRepo = new Mock<ITeamRepository>();
        var employeesRepo = new Mock<IEmployeeRepository>();
        var leaveBalancesRepo = new Mock<ILeaveBalanceRepository>();

        unitOfWork.SetupGet(x => x.Users).Returns(usersRepo.Object);
        unitOfWork.SetupGet(x => x.Companies).Returns(companiesRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentsRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitsRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamsRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeesRepo.Object);
        unitOfWork.SetupGet(x => x.LeaveBalances).Returns(leaveBalancesRepo.Object);

        usersRepo
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApplicationUser>());
        usersRepo
            .Setup(x => x.CreateUserAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        usersRepo
            .Setup(x => x.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        usersRepo
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        companiesRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Acme", YearlyVacationDays = 21 });

        employeesRepo
            .Setup(x => x.AddAsync(It.IsAny<Employee>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee employee, CancellationToken _) => employee);
        leaveBalancesRepo
            .Setup(x => x.AddAsync(It.IsAny<LeaveBalance>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaveBalance balance, CancellationToken _) => balance);

        unitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
