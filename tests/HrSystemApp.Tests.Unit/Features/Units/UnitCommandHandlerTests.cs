using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Units.Commands.CreateUnit;
using HrSystemApp.Application.Features.Units.Commands.DeleteUnit;
using HrSystemApp.Application.Features.Units.Commands.UpdateUnit;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using Moq;
using System.Linq.Expressions;
using DomainUnit = HrSystemApp.Domain.Models.Unit;

namespace HrSystemApp.Tests.Unit.Features.Units;

public class UnitCommandHandlerTests
{
    [Fact]
    public async Task CreateUnit_WhenDepartmentMissing_ReturnsNotFound()
    {
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        departmentRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        var sut = new CreateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateUnitCommand(Guid.NewGuid(), "Platform Unit", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task UpdateUnit_WhenDuplicateName_ReturnsAlreadyExists()
    {
        var unitId = Guid.NewGuid();
        var existing = new DomainUnit
        {
            Id = unitId,
            DepartmentId = Guid.NewGuid(),
            Name = "Unit A"
        };

        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<DomainUnit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new UpdateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateUnitCommand(unitId, "Unit B", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.AlreadyExists.Code);
    }

    [Fact]
    public async Task CreateUnit_WhenNameExists_ReturnsAlreadyExists()
    {
        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        departmentRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, CompanyId = Guid.NewGuid(), Name = "Eng" });
        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<DomainUnit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new CreateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateUnitCommand(departmentId, "Platform Unit", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.AlreadyExists.Code);
    }

    [Fact]
    public async Task CreateUnit_WhenValid_AddsAndSaves()
    {
        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        departmentRepo
            .Setup(x => x.GetByIdAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = departmentId, CompanyId = Guid.NewGuid(), Name = "Eng" });
        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<DomainUnit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        unitRepo
            .Setup(x => x.AddAsync(It.IsAny<DomainUnit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUnit unit, CancellationToken _) => unit);

        var sut = new CreateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateUnitCommand(departmentId, "Platform Unit", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        unitRepo.Verify(x => x.AddAsync(It.IsAny<DomainUnit>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUnit_WhenMissing_ReturnsNotFound()
    {
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUnit?)null);

        var sut = new UpdateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateUnitCommand(Guid.NewGuid(), "Unit X", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task UpdateUnit_WhenNameUnchanged_UpdatesWithoutNameCheck()
    {
        var unitId = Guid.NewGuid();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var existing = new DomainUnit
        {
            Id = unitId,
            DepartmentId = Guid.NewGuid(),
            Name = "Unit A",
            Description = "Before"
        };

        unitRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = new UpdateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateUnitCommand(unitId, "Unit A", "After", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        unitRepo.Verify(x => x.ExistsAsync(It.IsAny<Expression<Func<DomainUnit, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        unitRepo.Verify(x => x.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUnit_WhenNameChangedAndAvailable_UpdatesAndSaves()
    {
        var unitId = Guid.NewGuid();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var existing = new DomainUnit
        {
            Id = unitId,
            DepartmentId = Guid.NewGuid(),
            Name = "Unit A"
        };

        unitRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<DomainUnit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new UpdateUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateUnitCommand(unitId, "Unit B", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        unitRepo.Verify(x => x.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUnit_WhenMissing_ReturnsNotFound()
    {
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUnit?)null);

        var sut = new DeleteUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteUnitCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task DeleteUnit_WhenExists_DeletesAndSaves()
    {
        var entity = new DomainUnit { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Name = "Core" };
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        unitRepo
            .Setup(x => x.GetByIdAsync(entity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        unitRepo
            .Setup(x => x.DeleteAsync(entity, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new DeleteUnitCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteUnitCommand(entity.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        unitRepo.Verify(x => x.DeleteAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
