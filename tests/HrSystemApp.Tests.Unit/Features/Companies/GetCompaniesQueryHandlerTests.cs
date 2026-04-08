using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Companies;

public class GetCompaniesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsPagedCompaniesWithStatusCounts()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        companyRepo
            .Setup(x => x.GetPagedAsync("acme", CompanyStatus.Active, 1, 20, true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<Company>.Create(new List<Company>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompanyName = "Acme",
                    Status = CompanyStatus.Active
                }
            }, 1, 20, 1));

        companyRepo
            .Setup(x => x.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((4, 1, 0));

        var sut = new GetCompaniesQueryHandler(unitOfWork.Object);
        var query = new GetCompaniesQuery("acme", CompanyStatus.Active, 1, 20, IncludeLocations: true);

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalActive.Should().Be(4);
        result.Value.TotalInactive.Should().Be(1);
        result.Value.TotalSuspended.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNoCompanies_ReturnsEmptyPageWithCounters()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        companyRepo
            .Setup(x => x.GetPagedAsync(null, null, 2, 10, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<Company>.Create(Array.Empty<Company>(), 2, 10, 0));
        companyRepo
            .Setup(x => x.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 0, 0));

        var sut = new GetCompaniesQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetCompaniesQuery(null, null, 2, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.PageNumber.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.TotalActive.Should().Be(0);
        result.Value.TotalInactive.Should().Be(0);
        result.Value.TotalSuspended.Should().Be(0);
    }
}
