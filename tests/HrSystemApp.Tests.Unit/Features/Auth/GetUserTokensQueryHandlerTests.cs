using FluentAssertions;
using HrSystemApp.Application.Features.Auth.Queries.GetUserTokens;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Auth;

public class GetUserTokensQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsMappedTokenDtos()
    {
        var refreshTokenRepo = new Mock<IRefreshTokenRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.RefreshTokens).Returns(refreshTokenRepo.Object);

        var now = DateTime.UtcNow;
        refreshTokenRepo
            .Setup(x => x.GetActiveTokensByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshToken>
            {
                new()
                {
                    UserId = "user-1",
                    TokenHash = "hash-1",
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(7),
                    CreatedByIp = "127.0.0.1"
                }
            });

        var sut = new GetUserTokensQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUserTokensQuery("user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].TokenHash.Should().Be("hash-1");
        result.Value[0].CreatedByIp.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Handle_WhenNoTokens_ReturnsEmptyList()
    {
        var refreshTokenRepo = new Mock<IRefreshTokenRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.RefreshTokens).Returns(refreshTokenRepo.Object);

        refreshTokenRepo
            .Setup(x => x.GetActiveTokensByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RefreshToken>());

        var sut = new GetUserTokensQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetUserTokensQuery("user-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
