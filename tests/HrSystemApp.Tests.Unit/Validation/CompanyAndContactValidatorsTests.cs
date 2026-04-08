using FluentAssertions;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;
using HrSystemApp.Application.Features.ContactAdmin.Commands.CreateContactAdminRequest;

namespace HrSystemApp.Tests.Unit.Validation;

public class CompanyAndContactValidatorsTests
{
    [Fact]
    public void CreateCompany_EmptyName_ShouldFail()
    {
        var validator = new CreateCompanyCommandValidator();
        var command = new CreateCompanyCommand("", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "CompanyName");
    }

    [Fact]
    public void CreateCompanyLocation_EmptyCompanyId_ShouldFail()
    {
        var validator = new CreateCompanyLocationCommandValidator();
        var command = new CreateCompanyLocationCommand(Guid.Empty, "HQ", null, null, null);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "CompanyId");
    }

    [Fact]
    public void CreateContactAdminRequest_InvalidEmail_ShouldFail()
    {
        var validator = new CreateContactAdminRequestCommandValidator();
        var command = new CreateContactAdminRequestCommand("Name", "bad-email", "Company", "01000000000");

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == "Email");
    }
}
