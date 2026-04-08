using FluentAssertions;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Departments.Queries.GetDepartments;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Models;
using HrSystemApp.Tests.Unit.Common;
using Moq;
using System.Linq.Expressions;

namespace HrSystemApp.Tests.Unit.Features.Departments;

public class GetDepartmentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCompanyDoesNotExist_ReturnsNotFound()
    {
        var companyRepo = new Mock<ICompanyRepository>();
        var deptRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(deptRepo.Object);

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new GetDepartmentsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetDepartmentsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(DomainErrors.Company.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenCompanyExists_ReturnsDepartments()
    {
        MapsterTestConfig.EnsureInitialized();

        var companyRepo = new Mock<ICompanyRepository>();
        var deptRepo = new Mock<IDepartmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.Companies).Returns(companyRepo.Object);
        unitOfWork.SetupGet(x => x.Departments).Returns(deptRepo.Object);

        var companyId = Guid.NewGuid();

        companyRepo
            .Setup(x => x.ExistsAsync(It.IsAny<Expression<Func<Company, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        deptRepo
            .Setup(x => x.GetByCompanyAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Department>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Name = "HR"
                }
            });

        var sut = new GetDepartmentsQueryHandler(unitOfWork.Object);
        var result = await sut.Handle(new GetDepartmentsQuery(companyId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}
