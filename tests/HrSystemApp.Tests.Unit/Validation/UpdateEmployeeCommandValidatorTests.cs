using FluentAssertions;
using HrSystemApp.Application.Features.Employees.Commands.UpdateEmployee;

namespace HrSystemApp.Tests.Unit.Validation;

public class UpdateEmployeeCommandValidatorTests
{
    [Fact]
    public void Validate_WhenIdIsEmpty_ReturnsValidationError()
    {
        var validator = new UpdateEmployeeCommandValidator();
        var command = new UpdateEmployeeCommand(
            Guid.Empty,
            "Any Name",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validate_WhenFullNameTooLong_ReturnsValidationError()
    {
        var validator = new UpdateEmployeeCommandValidator();
        var command = new UpdateEmployeeCommand(
            Guid.NewGuid(),
            new string('A', 201),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FullName");
    }
}
