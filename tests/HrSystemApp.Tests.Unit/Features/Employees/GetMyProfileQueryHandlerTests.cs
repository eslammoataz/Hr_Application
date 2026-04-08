using FluentAssertions;
using HrSystemApp.Application.DTOs.Employees;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Employees.Queries.GetMyProfile;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Employees;

public class GetMyProfileQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenProfileMissing_ReturnsNotFound()
    {
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        employeeRepo
            .Setup(x => x.GetProfileByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeProfileDto?)null);

        var sut = new GetMyProfileQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetMyProfileQuery("user-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenProfileExists_ReturnsSuccess()
    {
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        var dto = new EmployeeProfileDto
        {
            Id = Guid.NewGuid(),
            FullName = "Profile User",
            Email = "profile@company.com"
        };

        employeeRepo
            .Setup(x => x.GetProfileByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var sut = new GetMyProfileQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetMyProfileQuery("user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Profile User");
    }
}
