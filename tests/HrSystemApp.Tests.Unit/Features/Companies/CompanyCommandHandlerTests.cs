using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Companies.Commands.ChangeCompanyStatus;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompany;
using HrSystemApp.Application.Features.Companies.Commands.CreateCompanyLocation;
using HrSystemApp.Application.Features.Companies.Commands.DeleteCompanyLocation;
using HrSystemApp.Application.Features.Companies.Commands.UpdateCompany;
using HrSystemApp.Application.Features.Companies.Commands.UpdateMyCompany;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Companies;

public class CompanyCommandHandlerTests
{
    [Fact]
    public async Task CreateCompany_WhenValid_AddsAndSaves()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        companyRepo
            .Setup(x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company company, CancellationToken _) => company);

        var sut = new CreateCompanyCommandHandler(unitOfWork.Object);
        var command = new CreateCompanyCommand("Acme", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        companyRepo.Verify(x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeCompanyStatus_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        companyRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new ChangeCompanyStatusCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new ChangeCompanyStatusCommand(Guid.NewGuid(), CompanyStatus.Suspended), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task ChangeCompanyStatus_WhenCompanyExists_UpdatesAndSaves()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyId = Guid.NewGuid();
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var company = new Company
        {
            Id = companyId,
            CompanyName = "Acme",
            Status = CompanyStatus.Active
        };

        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = new ChangeCompanyStatusCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new ChangeCompanyStatusCommand(companyId, CompanyStatus.Suspended), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        company.Status.Should().Be(CompanyStatus.Suspended);
        companyRepo.Verify(x => x.UpdateAsync(company, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompany_WhenCompanyMissing_ReturnsNotFound()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        companyRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(Guid.NewGuid(), "Updated", null, 25, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), 10, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task UpdateCompany_WhenNonSuperAdminAndUserMissing_ReturnsUnauthorized()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Target" });

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(companyId, "Updated", null, 25, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), 10, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task UpdateCompany_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var companyRepo = new Mock<ICompanyRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Target" });
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(companyId, "Updated", null, 25, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), 10, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task UpdateCompany_WhenUserCompanyMismatch_ReturnsForbidden()
    {
        var targetCompanyId = Guid.NewGuid();
        var userCompanyId = Guid.NewGuid();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var companyRepo = new Mock<ICompanyRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        companyRepo
            .Setup(x => x.GetByIdAsync(targetCompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = targetCompanyId, CompanyName = "Target" });
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = userCompanyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "E-1"
            });

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(targetCompanyId, "Updated", null, 25, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), 10, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.Forbidden.Code);
    }

    [Fact]
    public async Task UpdateCompany_WhenSuperAdmin_UpdatesAndSaves()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var company = new Company { Id = companyId, CompanyName = "Before", TimeZoneId = "UTC" };
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(companyId, "After", "logo", 30, new TimeSpan(7, 0, 0), new TimeSpan(15, 0, 0), 5, "Africa/Cairo");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        company.CompanyName.Should().Be("After");
        companyRepo.Verify(x => x.UpdateAsync(company, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompany_WhenSameCompanyEmployee_UpdatesAndSaves()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var companyRepo = new Mock<ICompanyRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var company = new Company { Id = companyId, CompanyName = "Before" };
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
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

        var sut = new UpdateCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateCompanyCommand(companyId, "After", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        companyRepo.Verify(x => x.UpdateAsync(company, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMyCompany_WhenUserMissing_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var sut = new UpdateMyCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateMyCompanyCommand("Acme", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task UpdateMyCompany_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new UpdateMyCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateMyCompanyCommand("Acme", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task UpdateMyCompany_WhenCompanyMissing_ReturnsNotFound()
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
                EmployeeCode = "E-2"
            });
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new UpdateMyCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateMyCompanyCommand("Acme", null, 21, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), 15, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task UpdateMyCompany_WhenValid_UpdatesAndSaves()
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
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var company = new Company { Id = companyId, CompanyName = "Before" };
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "E-3"
            });
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var sut = new UpdateMyCompanyCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new UpdateMyCompanyCommand("After", null, 30, new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), 10, "UTC");
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        company.CompanyName.Should().Be("After");
        companyRepo.Verify(x => x.UpdateAsync(company, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateCompanyLocation_WhenUnauthorized_ReturnsUnauthorized()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        var sut = new CreateCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var command = new CreateCompanyLocationCommand(Guid.NewGuid(), "HQ", "Cairo", 30.0, 31.0);
        var result = await sut.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task CreateCompanyLocation_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new CreateCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new CreateCompanyLocationCommand(companyId, "HQ", "Cairo", 30, 31), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task CreateCompanyLocation_WhenCompanyMismatch_ReturnsForbidden()
    {
        var targetCompanyId = Guid.NewGuid();
        var userCompanyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = userCompanyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "E-4"
            });

        var sut = new CreateCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new CreateCompanyLocationCommand(targetCompanyId, "HQ", "Cairo", 30, 31), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.Forbidden.Code);
    }

    [Fact]
    public async Task CreateCompanyLocation_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var sut = new CreateCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new CreateCompanyLocationCommand(companyId, "HQ", "Cairo", 30, 31), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task CreateCompanyLocation_WhenSuperAdminAndValid_AddsAndSaves()
    {
        var companyId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var companyRepo = new Mock<ICompanyRepository>();
        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        companyRepo
            .Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Acme" });
        locationRepo
            .Setup(x => x.AddAsync(It.IsAny<CompanyLocation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyLocation location, CancellationToken _) => location);

        var sut = new CreateCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new CreateCompanyLocationCommand(companyId, "HQ", "Cairo", 30, 31), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        locationRepo.Verify(x => x.AddAsync(It.IsAny<CompanyLocation>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCompanyLocation_WhenLocationMissing_ReturnsNotFound()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        locationRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyLocation?)null);

        var sut = new DeleteCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new DeleteCompanyLocationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task DeleteCompanyLocation_WhenUserMissing_ReturnsUnauthorized()
    {
        var locationId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns((string?)null);

        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        locationRepo
            .Setup(x => x.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyLocation
            {
                Id = locationId,
                CompanyId = Guid.NewGuid(),
                LocationName = "Branch"
            });

        var sut = new DeleteCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new DeleteCompanyLocationCommand(locationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Auth.Unauthorized.Code);
    }

    [Fact]
    public async Task DeleteCompanyLocation_WhenEmployeeMissing_ReturnsEmployeeNotFound()
    {
        var locationId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var locationRepo = new Mock<ICompanyLocationRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        locationRepo
            .Setup(x => x.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyLocation
            {
                Id = locationId,
                CompanyId = Guid.NewGuid(),
                LocationName = "Branch"
            });
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var sut = new DeleteCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new DeleteCompanyLocationCommand(locationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task DeleteCompanyLocation_WhenCompanyMismatch_ReturnsForbidden()
    {
        var locationId = Guid.NewGuid();
        var locationCompanyId = Guid.NewGuid();
        var userCompanyId = Guid.NewGuid();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.HR));
        currentUser.SetupGet(x => x.UserId).Returns("user-1");

        var locationRepo = new Mock<ICompanyLocationRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        locationRepo
            .Setup(x => x.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyLocation
            {
                Id = locationId,
                CompanyId = locationCompanyId,
                LocationName = "Branch"
            });
        employeeRepo
            .Setup(x => x.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = userCompanyId,
                FullName = "User",
                PhoneNumber = "010",
                Email = "u@co.com",
                EmployeeCode = "E-2"
            });

        var sut = new DeleteCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new DeleteCompanyLocationCommand(locationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.Forbidden.Code);
    }

    [Fact]
    public async Task DeleteCompanyLocation_WhenSuperAdmin_DeletesAndSaves()
    {
        var locationId = Guid.NewGuid();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.Role).Returns(nameof(UserRole.SuperAdmin));

        var locationRepo = new Mock<ICompanyLocationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.CompanyLocations).Returns(locationRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var location = new CompanyLocation
        {
            Id = locationId,
            CompanyId = Guid.NewGuid(),
            LocationName = "HQ"
        };

        locationRepo
            .Setup(x => x.GetByIdAsync(locationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        var sut = new DeleteCompanyLocationCommandHandler(unitOfWork.Object, currentUser.Object);
        var result = await sut.Handle(new DeleteCompanyLocationCommand(locationId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(locationId);
        locationRepo.Verify(x => x.DeleteAsync(location, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
