using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Units.Queries.GetUnits;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;
using System.Linq.Expressions;

namespace HrSystemApp.Tests.Unit.Features.Units;

public class GetUnitsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsNotFound()
    {
        var deptRepo = new Mock<IDepartmentRepository>();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(deptRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        deptRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new GetUnitsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUnitsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenDepartmentExists_ReturnsUnits()
    {
        MapsterTestConfig.EnsureInitialized();

        var departmentId = Guid.NewGuid();
        var deptRepo = new Mock<IDepartmentRepository>();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(deptRepo.Object);
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        deptRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Department, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        unitRepo
            .Setup(x => x.GetByDepartmentAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HrSystemApp.Domain.Models.Unit>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DepartmentId = departmentId,
                    Name = "People Ops"
                }
            });

        var sut = new GetUnitsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUnitsQuery(departmentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}
