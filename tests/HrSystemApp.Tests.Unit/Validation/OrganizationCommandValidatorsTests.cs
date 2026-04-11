using FluentAssertions;
using HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;
using HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;
using HrSystemApp.Application.Features.Employees.Commands.AssignEmployeeToTeam;
using HrSystemApp.Application.Features.Employees.Commands.CreateEmployee;
using HrSystemApp.Application.Features.Teams.Commands.CreateTeam;
using HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;
using HrSystemApp.Application.Features.Units.Commands.CreateUnit;
using HrSystemApp.Application.Features.Units.Commands.UpdateUnit;
using HrSystemApp.Domain.Enums;

namespace HrSystemApp.Tests.Unit.Validation;

public class OrganizationCommandValidatorsTests
{
    [Fact]
    public void CreateDepartment_EmptyRequiredFields_ShouldFail()
    {
        var validator = new CreateDepartmentCommandValidator();
        var command = new CreateDepartmentCommand(Guid.Empty, "", null, null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "CompanyId");
        result.Errors.Should().Contain(x => x.PropertyName == "Name");
    }

    [Fact]
    public void UpdateDepartment_EmptyId_ShouldFail()
    {
        var validator = new UpdateDepartmentCommandValidator();
        var command = new UpdateDepartmentCommand(Guid.Empty, "Dept", null, null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Id");
    }

    [Fact]
    public void AssignEmployeeToTeam_EmptyIds_ShouldFail()
    {
        var validator = new AssignEmployeeToTeamCommandValidator();
        var command = new AssignEmployeeToTeamCommand(Guid.Empty, Guid.Empty);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "EmployeeId");
        result.Errors.Should().Contain(x => x.PropertyName == "TeamId");
    }

    [Fact]
    public void CreateEmployee_InvalidPhone_ShouldFail()
    {
        var validator = new CreateEmployeeCommandValidator();
        var command = new CreateEmployeeCommand("Emp Name", "emp@corp.com", "abc", Guid.NewGuid(), UserRole.Employee);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "PhoneNumber");
    }

    [Fact]
    public void CreateEmployee_TeamLeaderWithoutTeamId_ShouldFail()
    {
        var validator = new CreateEmployeeCommandValidator();
        var command = new CreateEmployeeCommand("Emp Name", "emp@corp.com", "01234567890", Guid.NewGuid(), UserRole.TeamLeader);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "TeamId");
    }

    [Fact]
    public void CreateEmployee_UnitLeaderWithoutUnitId_ShouldFail()
    {
        var validator = new CreateEmployeeCommandValidator();
        var command = new CreateEmployeeCommand("Emp Name", "emp@corp.com", "01234567890", Guid.NewGuid(), UserRole.UnitLeader);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "UnitId");
    }

    [Fact]
    public void CreateEmployee_VicePresidentWithoutDepartmentId_ShouldFail()
    {
        var validator = new CreateEmployeeCommandValidator();
        var command = new CreateEmployeeCommand("Emp Name", "emp@corp.com", "01234567890", Guid.NewGuid(), UserRole.VicePresident);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "DepartmentId");
    }

    [Fact]
    public void CreateTeam_EmptyUnitId_ShouldFail()
    {
        var validator = new CreateTeamCommandValidator();
        var command = new CreateTeamCommand(Guid.Empty, "Team", null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "UnitId");
    }

    [Fact]
    public void UpdateTeam_EmptyId_ShouldFail()
    {
        var validator = new UpdateTeamCommandValidator();
        var command = new UpdateTeamCommand(Guid.Empty, "Team", null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Id");
    }

    [Fact]
    public void CreateUnit_EmptyDepartmentId_ShouldFail()
    {
        var validator = new CreateUnitCommandValidator();
        var command = new CreateUnitCommand(Guid.Empty, "Unit", null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "DepartmentId");
    }

    [Fact]
    public void UpdateUnit_EmptyId_ShouldFail()
    {
        var validator = new UpdateUnitCommandValidator();
        var command = new UpdateUnitCommand(Guid.Empty, "Unit", null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Id");
    }
}
