using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Teams.Commands.CreateTeam;
using HrSystemApp.Application.Features.Teams.Commands.DeleteTeam;
using HrSystemApp.Application.Features.Teams.Commands.UpdateTeam;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using Moq;
using System.Linq.Expressions;

namespace HrSystemApp.Tests.Unit.Features.Teams;

public class TeamCommandHandlerTests
{
    [Fact]
    public async Task CreateTeam_WhenUnitMissing_ReturnsUnitNotFound()
    {
        var unitRepo = new Mock<IUnitRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HrSystemApp.Domain.Models.Unit?)null);

        var sut = new CreateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateTeamCommand(Guid.NewGuid(), "API Team", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Unit.NotFound.Code);
    }

    [Fact]
    public async Task CreateTeam_WhenNameExists_ReturnsAlreadyExists()
    {
        var unitId = Guid.NewGuid();
        var unit = new HrSystemApp.Domain.Models.Unit { Id = unitId, DepartmentId = Guid.NewGuid(), Name = "Platform Unit" };
        var unitRepo = new Mock<IUnitRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        unitRepo
            .Setup(x => x.GetByIdAsync(unitId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);
        teamRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Team, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new CreateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateTeamCommand(unitId, "API Team", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.AlreadyExists.Code);
    }

    [Fact]
    public async Task CreateTeam_WhenValid_AddsAndSaves()
    {
        var unit = new HrSystemApp.Domain.Models.Unit { Id = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), Name = "Platform Unit" };
        var unitRepo = new Mock<IUnitRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Units).Returns(unitRepo.Object);
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        unitRepo
            .Setup(x => x.GetByIdAsync(unit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);
        teamRepo
            .Setup(x => x.ExistsAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        teamRepo
            .Setup(x => x.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team t, CancellationToken _) => t);

        var sut = new CreateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new CreateTeamCommand(unit.Id, "API Team", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        teamRepo.Verify(x => x.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTeam_WhenNameConflict_ReturnsAlreadyExists()
    {
        var teamId = Guid.NewGuid();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        teamRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team
            {
                Id = teamId,
                UnitId = Guid.NewGuid(),
                Name = "Before"
            });
        teamRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Team, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new UpdateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateTeamCommand(teamId, "After", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.AlreadyExists.Code);
    }

    [Fact]
    public async Task UpdateTeam_WhenMissing_ReturnsNotFound()
    {
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        teamRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var sut = new UpdateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateTeamCommand(Guid.NewGuid(), "Renamed Team", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.NotFound.Code);
    }

    [Fact]
    public async Task UpdateTeam_WhenNameUnchanged_UpdatesWithoutNameCheck()
    {
        var teamId = Guid.NewGuid();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var team = new Team
        {
            Id = teamId,
            UnitId = Guid.NewGuid(),
            Name = "Same",
            Description = "Before"
        };

        teamRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var sut = new UpdateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateTeamCommand(teamId, "Same", "After", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        teamRepo.Verify(x => x.ExistsAsync(It.IsAny<Expression<Func<Team, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
        teamRepo.Verify(x => x.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTeam_WhenNameChangedAndAvailable_UpdatesAndSaves()
    {
        var teamId = Guid.NewGuid();
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var team = new Team
        {
            Id = teamId,
            UnitId = Guid.NewGuid(),
            Name = "Before"
        };

        teamRepo
            .Setup(x => x.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Team, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new UpdateTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new UpdateTeamCommand(teamId, "After", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        teamRepo.Verify(x => x.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTeam_WhenMissing_ReturnsNotFound()
    {
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);

        teamRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var sut = new DeleteTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteTeamCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Team.NotFound.Code);
    }

    [Fact]
    public async Task DeleteTeam_WhenExists_DeletesAndSaves()
    {
        var team = new Team { Id = Guid.NewGuid(), UnitId = Guid.NewGuid(), Name = "API Team" };
        var teamRepo = new Mock<ITeamRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Teams).Returns(teamRepo.Object);
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        teamRepo
            .Setup(x => x.GetByIdAsync(team.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var sut = new DeleteTeamCommandHandler(unitOfWork.Object);
        var result = await sut.Handle(new DeleteTeamCommand(team.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        teamRepo.Verify(x => x.DeleteAsync(team, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
