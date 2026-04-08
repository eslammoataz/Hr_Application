using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Teams.Queries.GetTeams;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;
using System.Linq.Expressions;

namespace HrSystemApp.Tests.Unit.Features.Teams;

public class GetTeamsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnitNotFound_ReturnsNotFound()
    {
        var unitRepo = new Mock<IUnitRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<HrSystemApp.Domain.Models.Unit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new GetTeamsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetTeamsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenUnitExists_ReturnsTeams()
    {
        MapsterTestConfig.EnsureInitialized();

        var unitId = Guid.NewGuid();
        var unitRepo = new Mock<IUnitRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        unitRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<HrSystemApp.Domain.Models.Unit, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        teamRepo
            .Setup(x => x.GetByUnitAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    UnitId = unitId,
                    Name = "Platform"
                }
            });

        var sut = new GetTeamsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetTeamsQuery(unitId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}
