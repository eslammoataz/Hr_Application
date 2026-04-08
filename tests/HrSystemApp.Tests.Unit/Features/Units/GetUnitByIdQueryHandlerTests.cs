using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Units.Queries.GetUnitById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Tests.Unit.Common;
using Moq;
using DomainUnit = HrSystemApp.Domain.Models.Unit;

namespace HrSystemApp.Tests.Unit.Features.Units;

public class GetUnitByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnitNotFound_ReturnsNotFound()
    {
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainUnit?)null);

        var sut = new GetUnitByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUnitByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenUnitExists_ReturnsMappedResponse()
    {
        MapsterTestConfig.EnsureInitialized();

        var unitId = Guid.NewGuid();
        var unitRepo = new Mock<IUnitRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomainUnit
            {
                Id = unitId,
                Name = "Core Unit",
                DepartmentId = Guid.NewGuid()
            });

        var sut = new GetUnitByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUnitByIdQuery(unitId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Core Unit");
    }
}
