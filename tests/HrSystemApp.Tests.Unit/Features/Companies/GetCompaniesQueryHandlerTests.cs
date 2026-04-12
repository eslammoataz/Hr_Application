using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.DTOs.Departments;
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
            .ReturnsAsync(new CompaniesPagedResult
            {
                Items = new List<CompanyResponse> 
                { 
                    new CompanyResponse(
                        Guid.NewGuid(), 
                        "Acme", 
                        null, 
                        21, 
                        TimeSpan.FromHours(9), 
                        TimeSpan.FromHours(17), 
                        15, 
                        "UTC", 
                        CompanyStatus.Active.ToString(), 
                        new List<CompanyLocationResponse>(), 
                        new List<DepartmentResponse>()) 
                },
                PageNumber = 1,
                PageSize = 20,
                TotalCount = 1,
                TotalActive = 4,
                TotalInactive = 1,
                TotalSuspended = 0
            });

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
            .ReturnsAsync(new CompaniesPagedResult
            {
                Items = new List<CompanyResponse>(),
                PageNumber = 2,
                PageSize = 10,
                TotalCount = 0,
                TotalActive = 0,
                TotalInactive = 0,
                TotalSuspended = 0
            });

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
