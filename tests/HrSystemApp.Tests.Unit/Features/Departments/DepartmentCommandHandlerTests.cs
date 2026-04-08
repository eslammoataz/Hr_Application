using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Departments.Commands.CreateDepartment;
using HrSystemApp.Application.Features.Departments.Commands.DeleteDepartment;
using HrSystemApp.Application.Features.Departments.Commands.UpdateDepartment;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using Moq;
using System.Linq.Expressions;

namespace HrSystemApp.Tests.Unit.Features.Departments;

public class DepartmentCommandHandlerTests
{
    [Fact]
    public async Task CreateDepartment_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new CreateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateDepartmentCommand(Guid.NewGuid(), "Engineering", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Company.NotFound.Code);
    }

    [Fact]
    public async Task CreateDepartment_WhenValid_AddsAndSaves()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        departmentRepo
            .Setup(x => x.AddAsync(It.IsAny<Department>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department d, CancellationToken _) => d);

        var sut = new CreateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateDepartmentCommand(Guid.NewGuid(), "Engineering", "Core", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        departmentRepo.Verify(x => x.AddAsync(It.IsAny<Department>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateDepartment_WhenNameAlreadyExists_ReturnsAlreadyExists()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new CreateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateDepartmentCommand(Guid.NewGuid(), "Engineering", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.AlreadyExists.Code);
    }

    [Fact]
    public async Task CreateDepartment_WhenManagerMissing_ReturnsEmployeeNotFound()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        employeeRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Employee, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new CreateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateDepartmentCommand(Guid.NewGuid(), "Engineering", null, null, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task CreateDepartment_WhenVicePresidentMissing_ReturnsEmployeeNotFound()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var employeeRepo = new Mock<IEmployeeRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Employees).Returns(employeeRepo.Object);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        employeeRepo
            .SetupSequence(x => x.ExistsAsync(It.IsAny<Expression<Func<Employee, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        var sut = new CreateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateDepartmentCommand(Guid.NewGuid(), "Engineering", null, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Employee.NotFound.Code);
    }

    [Fact]
    public async Task UpdateDepartment_WhenMissing_ReturnsNotFound()
    {
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        departmentRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        var sut = new UpdateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateDepartmentCommand(Guid.NewGuid(), "Updated", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task UpdateDepartment_WhenNameConflict_ReturnsAlreadyExists()
    {
        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        departmentRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department
            {
                Id = departmentId,
                CompanyId = Guid.NewGuid(),
                Name = "Old"
            });
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new UpdateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateDepartmentCommand(departmentId, "New", null, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.AlreadyExists.Code);
    }

    [Fact]
    public async Task UpdateDepartment_WhenNameUnchanged_UpdatesWithoutNameCheck()
    {
        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var department = new Department
        {
            Id = departmentId,
            CompanyId = Guid.NewGuid(),
            Name = "Same",
            Description = "Before"
        };
        departmentRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(department);

        var sut = new UpdateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateDepartmentCommand(departmentId, "Same", "After", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        departmentRepo.Verify(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        departmentRepo.Verify(x => x.UpdateAsync(department, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDepartment_WhenNameChangedAndAvailable_UpdatesAndSaves()
    {
        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var department = new Department
        {
            Id = departmentId,
            CompanyId = Guid.NewGuid(),
            Name = "Before"
        };
        departmentRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(department);
        departmentRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new UpdateDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateDepartmentCommand(departmentId, "After", null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        departmentRepo.Verify(x => x.UpdateAsync(department, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDepartment_WhenMissing_ReturnsNotFound()
    {
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        departmentRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        var sut = new DeleteDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteDepartmentCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task DeleteDepartment_WhenExists_DeletesAndSaves()
    {
        var department = new Department { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Name = "Ops" };
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        departmentRepo
            .Setup(x => x.GetByIdAsync(department.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(department);
        departmentRepo
            .Setup(x => x.DeleteAsync(department, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new DeleteDepartmentCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteDepartmentCommand(department.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        departmentRepo.Verify(x => x.DeleteAsync(department, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
