using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Teams.Queries.GetTeamById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Teams;

public class GetTeamByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFound()
    {
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        teamRepo
            .Setup(x => x.GetWithMembersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var sut = new GetTeamByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetTeamByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenTeamExists_ReturnsMappedResponse()
    {
        MapsterTestConfig.EnsureInitialized();

        var teamId = Guid.NewGuid();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        teamRepo
            .Setup(x => x.GetWithMembersAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team
            {
                Id = teamId,
                Name = "Platform",
                UnitId = Guid.NewGuid()
            });

        var sut = new GetTeamByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetTeamByIdQuery(teamId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Platform");
    }
}
