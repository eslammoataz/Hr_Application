using FluentAssertions;
using HrSystemApp.Application.Features.Requests.Queries.GetCompanyRequests;
using HrSystemApp.Application.Features.Requests.Queries.GetPendingApprovals;
using HrSystemApp.Application.Features.Requests.Queries.GetUserRequests;

namespace HrSystemApp.Tests.Unit.Features.Requests;

/// <summary>
/// Tests for the MaxPageSize = 100 cap added to GetUserRequestsQuery,
/// GetCompanyRequestsQuery, and GetPendingApprovalsQuery (fix for M-1).
/// </summary>
public class PageSizeCapTests
{
    // ─── GetUserRequestsQuery ────────────────────────────────────────────────

    [Fact]
    public void GetUserRequestsQuery_DefaultPageSize_IsTen()
    {
        var query = new GetUserRequestsQuery();
        query.PageSize.Should().Be(10);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeWithinLimit_IsPreserved()
    {
        var query = new GetUserRequestsQuery { PageSize = 50 };
        query.PageSize.Should().Be(50);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeAtExactLimit_IsPreserved()
    {
        var query = new GetUserRequestsQuery { PageSize = 100 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeExceedsLimit_IsCappedAt100()
    {
        var query = new GetUserRequestsQuery { PageSize = 101 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeVeryLarge_IsCappedAt100()
    {
        var query = new GetUserRequestsQuery { PageSize = 1_000_000 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeZero_IsClampedToOne()
    {
        var query = new GetUserRequestsQuery { PageSize = 0 };
        query.PageSize.Should().Be(1);
    }

    [Fact]
    public void GetUserRequestsQuery_PageSizeNegative_IsClampedToOne()
    {
        var query = new GetUserRequestsQuery { PageSize = -10 };
        query.PageSize.Should().Be(1);
    }

    // ─── GetCompanyRequestsQuery ─────────────────────────────────────────────

    [Fact]
    public void GetCompanyRequestsQuery_DefaultPageSize_IsTen()
    {
        var query = new GetCompanyRequestsQuery();
        query.PageSize.Should().Be(10);
    }

    [Fact]
    public void GetCompanyRequestsQuery_PageSizeWithinLimit_IsPreserved()
    {
        var query = new GetCompanyRequestsQuery { PageSize = 75 };
        query.PageSize.Should().Be(75);
    }

    [Fact]
    public void GetCompanyRequestsQuery_PageSizeAtExactLimit_IsPreserved()
    {
        var query = new GetCompanyRequestsQuery { PageSize = 100 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetCompanyRequestsQuery_PageSizeExceedsLimit_IsCappedAt100()
    {
        var query = new GetCompanyRequestsQuery { PageSize = 500 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetCompanyRequestsQuery_PageSizeZero_IsClampedToOne()
    {
        var query = new GetCompanyRequestsQuery { PageSize = 0 };
        query.PageSize.Should().Be(1);
    }

    [Fact]
    public void GetCompanyRequestsQuery_PageSizeNegative_IsClampedToOne()
    {
        var query = new GetCompanyRequestsQuery { PageSize = -1 };
        query.PageSize.Should().Be(1);
    }

    // ─── GetPendingApprovalsQuery ─────────────────────────────────────────────

    [Fact]
    public void GetPendingApprovalsQuery_DefaultPageSize_IsTen()
    {
        var query = new GetPendingApprovalsQuery();
        query.PageSize.Should().Be(10);
    }

    [Fact]
    public void GetPendingApprovalsQuery_PageSizeWithinLimit_IsPreserved()
    {
        var query = new GetPendingApprovalsQuery { PageSize = 20 };
        query.PageSize.Should().Be(20);
    }

    [Fact]
    public void GetPendingApprovalsQuery_PageSizeAtExactLimit_IsPreserved()
    {
        var query = new GetPendingApprovalsQuery { PageSize = 100 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetPendingApprovalsQuery_PageSizeExceedsLimit_IsCappedAt100()
    {
        var query = new GetPendingApprovalsQuery { PageSize = 9999 };
        query.PageSize.Should().Be(100);
    }

    [Fact]
    public void GetPendingApprovalsQuery_PageSizeZero_IsClampedToOne()
    {
        var query = new GetPendingApprovalsQuery { PageSize = 0 };
        query.PageSize.Should().Be(1);
    }

    [Fact]
    public void GetPendingApprovalsQuery_PageSizeNegative_IsClampedToOne()
    {
        var query = new GetPendingApprovalsQuery { PageSize = -100 };
        query.PageSize.Should().Be(1);
    }
}
