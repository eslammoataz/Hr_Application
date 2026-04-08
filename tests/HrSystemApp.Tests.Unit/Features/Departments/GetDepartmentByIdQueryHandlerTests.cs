using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Departments.Queries.GetDepartmentById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Departments;

public class GetDepartmentByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenDepartmentNotFound_ReturnsNotFound()
    {
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        departmentRepo
            .Setup(x => x.GetWithUnitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Department?)null);

        var sut = new GetDepartmentByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetDepartmentByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Department.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenDepartmentExists_ReturnsMappedResponse()
    {
        MapsterTestConfig.EnsureInitialized();

        var departmentId = Guid.NewGuid();
        var departmentRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Departments).Returns(departmentRepo.Object);

        departmentRepo
            .Setup(x => x.GetWithUnitsAsync(departmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department
            {
                Id = departmentId,
                CompanyId = Guid.NewGuid(),
                Name = "Engineering"
            });

        var sut = new GetDepartmentByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetDepartmentByIdQuery(departmentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Engineering");
    }
}
