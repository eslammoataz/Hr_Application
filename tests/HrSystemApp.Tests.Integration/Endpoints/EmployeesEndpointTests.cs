using System.Net;
using System.Text.Json;
using FluentAssertions;
using HrSystemApp.Api.Authorization;
using HrSystemApp.Tests.Integration.Infrastructure;

namespace HrSystemApp.Tests.Integration.Endpoints;

[Collection("Integration")]
public class EmployeesEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public EmployeesEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetEmployees_WithoutToken_ReturnsUnauthorized()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/employees?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEmployees_WithSearchAndPaging_ReturnsFilteredEmployees()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Search Corp");
        await _fixture.SeedEmployeeAsync(companyId, "user-a", "Alice Walker", "alice@corp.com");
        await _fixture.SeedEmployeeAsync(companyId, "user-b", "Bob Kane", "bob@corp.com");

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR, companyId);

        var response = await client.GetAsync("/api/employees?companyId=" + companyId + "&search=alice&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("fullName").GetString().Should().Contain("Alice");
    }

    [Fact]
    public async Task GetEmployees_ReturnsEmployeeRole_WhenUserHasRole()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Role Corp");
        await _fixture.SeedEmployeeAsync(companyId, "role-user", "Role User", "role.user@corp.com");
        await _fixture.AssignRoleToUserAsync("role-user", Roles.HR);

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR, companyId);

        var response = await client.GetAsync("/api/employees?companyId=" + companyId + "&search=role.user@corp.com&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("role").GetString().Should().Be(Roles.HR);
    }

    [Fact]
    public async Task GetEmployees_WithPaging_ReturnsExpectedPagingMetadata()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Paging Corp");
        await _fixture.SeedEmployeeAsync(companyId, "paging-user-1", "Paging User One", "paging.user1@corp.com");
        await _fixture.SeedEmployeeAsync(companyId, "paging-user-2", "Paging User Two", "paging.user2@corp.com");
        await _fixture.SeedEmployeeAsync(companyId, "paging-user-3", "Paging User Three", "paging.user3@corp.com");

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR, companyId);

        var response = await client.GetAsync("/api/employees?companyId=" + companyId + "&page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        data.GetProperty("items").GetArrayLength().Should().Be(2);
        data.GetProperty("pageNumber").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(2);
        data.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetEmployees_ReturnsEmptyRole_WhenUserHasNoRole()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("No Role Corp");
        await _fixture.SeedEmployeeAsync(companyId, "norole-user", "No Role User", "norole.user@corp.com");

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR, companyId);

        var response = await client.GetAsync("/api/employees?companyId=" + companyId + "&search=norole.user@corp.com&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("role").GetString().Should().Be(string.Empty);
    }

    [Fact]
    public async Task GetEmployees_WithRoleAndStatusFilter_ReturnsCountersIgnoringStatusFilter()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Filter Corp");
        await _fixture.SeedEmployeeAsync(
            companyId,
            "filter-user-active",
            "Filter User Active",
            "active@filter.corp",
            HrSystemApp.Domain.Enums.EmploymentStatus.Active);
        await _fixture.SeedEmployeeAsync(
            companyId,
            "filter-user-inactive",
            "Filter User Inactive",
            "inactive@filter.corp",
            HrSystemApp.Domain.Enums.EmploymentStatus.Inactive);

        await _fixture.AssignRoleToUserAsync("filter-user-active", Roles.HR);
        await _fixture.AssignRoleToUserAsync("filter-user-inactive", Roles.HR);

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR, companyId);
        var response = await client.GetAsync(
            "/api/employees?role=HR&status=Active&search=Filter&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        data.GetProperty("totalCount").GetInt32().Should().Be(1);
        data.GetProperty("totalActive").GetInt32().Should().Be(1);
        data.GetProperty("totalInactive").GetInt32().Should().Be(1);
        data.GetProperty("items").GetArrayLength().Should().Be(1);
        data.GetProperty("items")[0].GetProperty("employmentStatus").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task GetEmployees_CompanyAdminCannotOverrideCompanyScope()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyA = await _fixture.SeedCompanyAsync("Scope A");
        var companyB = await _fixture.SeedCompanyAsync("Scope B");

        await _fixture.SeedEmployeeAsync(companyA, "scope-user-a", "Scoped User A", "a@scope.corp");
        await _fixture.SeedEmployeeAsync(companyB, "scope-user-b", "Scoped User B", "b@scope.corp");

        using var client = _fixture.CreateAuthenticatedClient("company-admin-user", Roles.CompanyAdmin, companyA);
        var response = await client.GetAsync($"/api/employees?companyId={companyB}&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("fullName").GetString().Should().Be("Scoped User A");
        items[0].GetProperty("companyId").GetGuid().Should().Be(companyA);
    }

    [Fact]
    public async Task GetEmployeeById_WhenMissing_ReturnsNotFound()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR);

        var response = await client.GetAsync($"/api/employees/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyProfile_WhenNoEmployeeForUser_ReturnsNotFound()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.CreateAuthenticatedClient("profile-only-user", Roles.Employee);

        var response = await client.GetAsync("/api/employees/me/profile");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyBalances_WhenNoEmployeeForUser_ReturnsNotFound()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.CreateAuthenticatedClient("balances-only-user", Roles.Employee);

        var response = await client.GetAsync("/api/employees/me/balances");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
