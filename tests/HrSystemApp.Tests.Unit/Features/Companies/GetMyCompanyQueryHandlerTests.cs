using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Companies.Queries.GetMyCompany;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Companies;

public class GetMyCompanyQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new GetMyCompanyQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyCompanyQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task Handle_WhenEmployeeExistsAndCompanyExists_ReturnsCompany()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyId = Guid.NewGuid();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "EMP-1"
            });

        companyRepo
            .Setup(x => x.GetWithDetailsAsync(companyId, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company
            {
                Id = companyId,
                CompanyName = "Acme"
            });

        var sut = new GetMyCompanyQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyCompanyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Acme");
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

        var sut = new GetMyCompanyQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyCompanyQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "EMP-1"
            });
        companyRepo
            .Setup(x => x.GetWithDetailsAsync(companyId, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new GetMyCompanyQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetMyCompanyQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }
}
