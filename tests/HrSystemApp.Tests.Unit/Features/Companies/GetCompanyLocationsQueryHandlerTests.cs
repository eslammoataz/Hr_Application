using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanyLocations;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Companies;

public class GetCompanyLocationsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);
        currentUser.SetupGet(x => x.Role).Returns((string?)null);

        var unitOfWork = new Mock<IUnitOfWork>();

        var sut = new GetCompanyLocationsQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetCompanyLocationsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task Handle_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");
        currentUser.SetupGet(x => x.Role).Returns(nameof(HrSystemApp.Domain.Enums.UserRole.HR));

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new GetCompanyLocationsQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetCompanyLocationsQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenTargetCompanyMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns("SuperAdmin");

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new GetCompanyLocationsQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetCompanyLocationsQuery(companyId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSuperAdminAndCompanyExists_ReturnsLocations()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns("SuperAdmin");

        var companyRepo = new Mock<ICompanyRepository>();
        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);

        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Acme" });

        locationRepo
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CompanyLocation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyLocation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    LocationName = "HQ"
                }
            });

        var sut = new GetCompanyLocationsQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetCompanyLocationsQuery(companyId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].LocationName.Should().Be("HQ");
    }

    [Fact]
    public async Task Handle_WhenRegularUserAndCompanyExists_ReturnsLocations()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");
        currentUser.SetupGet(x => x.Role).Returns(nameof(HrSystemApp.Domain.Enums.UserRole.HR));

        var employeeRepo = new Mock<IEmployeeRepository>();
        var companyRepo = new Mock<ICompanyRepository>();
        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);

        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "E-1"
            });
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Acme" });
        locationRepo
            .Setup(x => x.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<CompanyLocation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompanyLocation>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    LocationName = "Main"
                }
            });

        var sut = new GetCompanyLocationsQueryHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new GetCompanyLocationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].LocationName.Should().Be("Main");
    }
}
