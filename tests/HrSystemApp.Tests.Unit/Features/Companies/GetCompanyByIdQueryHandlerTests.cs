using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Companies.Queries.GetCompanyById;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.Companies;

public class GetCompanyByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCompanyNotFound_ReturnsNotFoundError()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        companyRepo
            .Setup(x => x.GetWithDetailsAsync(It.IsAny<Guid>(), false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HrSystemApp.Domain.Models.Company?)null);

        var sut = new GetCompanyByIdQueryHandler(unitOfWork.Object);
        var query = new GetCompanyByIdQuery(Guid.NewGuid());

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.General.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenCompanyExists_ReturnsSuccess()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyId = Guid.NewGuid();
        var companyRepo = new Mock<ICompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);

        companyRepo
            .Setup(x => x.GetWithDetailsAsync(companyId, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company
            {
                Id = companyId,
                CompanyName = "Acme"
            });

        var sut = new GetCompanyByIdQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetCompanyByIdQuery(companyId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Acme");
    }
}
