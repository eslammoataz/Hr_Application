using FluentAssertions;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Features.ContactAdmin.Queries.GetContactAdminRequests;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Application.Interfaces.Repositories;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using Moq;

namespace HrSystemApp.Tests.Unit.Features.ContactAdmin;

public class GetContactAdminRequestsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenCalled_ReturnsPagedResultAndStatusCounters()
    {
        var repo = new Mock<IContactAdminRequestRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(x => x.ContactAdminRequests).Returns(repo.Object);

        var entities = new List<ContactAdminRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Requester",
                Email = "req@co.com",
                CompanyName = "Acme",
                PhoneNumber = "010",
                Status = ContactAdminRequestStatus.Pending
            }
        };

        repo.Setup(x => x.GetPagedAsync(null, true, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PagedResult<ContactAdminRequest>.Create(entities, 1, 20, 1));
        repo.Setup(x => x.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, 2, 1));

        var sut = new GetContactAdminRequestsQueryHandler(unitOfWork.Object);
        var query = new GetContactAdminRequestsQuery(IsPending: true, PageNumber: 1, PageSize: 20);

        var result = await sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalPending.Should().Be(3);
        result.Value.TotalAccepted.Should().Be(2);
        result.Value.TotalRejected.Should().Be(1);
    }
}
