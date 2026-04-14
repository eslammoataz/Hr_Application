using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HrSystemApp.Api.Authorization;
using HrSystemApp.Tests.Integration.Infrastructure;

namespace HrSystemApp.Tests.Integration.Endpoints;

[Collection("Integration")]
public class CompaniesEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public CompaniesEndpointTests(IntegrationTestFixture fixture)
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
    public async Task GetCompanies_WithSuperAdminRole_ReturnsPagedCompaniesAndStats()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        await _fixture.SeedCompanyAsync("Acme");
        await _fixture.SeedCompanyAsync("Globex");

        using var client = _fixture.CreateAuthenticatedClient("super-user", Roles.SuperAdmin);

        var response = await client.GetAsync("/api/companies?pageNumber=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        data.GetProperty("totalActive").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task CompanyLocations_PostThenGet_ReturnsPersistedLocation()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Locations Corp");
        await _fixture.SeedEmployeeAsync(companyId, "company-admin-user", "Company Admin", "admin@loc.com");

        using var client = _fixture.CreateAuthenticatedClient("company-admin-user", Roles.Executive);

        var payload = """
                      {
                        "locationName": "HQ",
                        "address": "Main St",
                        "latitude": 30.0444,
                        "longitude": 31.2357
                      }
                      """;

        var postResponse = await client.PostAsync(
            $"/api/companies/{companyId}/locations",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        postResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await client.GetAsync($"/api/companies/{companyId}/locations");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
        var locations = doc.RootElement.GetProperty("data");
        locations.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        locations.EnumerateArray().Any(x => x.GetProperty("locationName").GetString() == "HQ").Should().BeTrue();
    }

    [Fact]
    public async Task CompanyLocations_Post_WithDifferentCompanyAdmin_IsDenied()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var adminCompanyId = await _fixture.SeedCompanyAsync("Admin Company");
        var targetCompanyId = await _fixture.SeedCompanyAsync("Target Company");
        await _fixture.SeedEmployeeAsync(adminCompanyId, "foreign-admin-user", "Foreign Admin", "foreign-admin@loc.com");

        using var client = _fixture.CreateAuthenticatedClient("foreign-admin-user", Roles.Executive);

        var payload = """
                      {
                        "locationName": "HQ",
                        "address": "Main St",
                        "latitude": 30.0444,
                        "longitude": 31.2357
                      }
                      """;

        var response = await client.PostAsync(
            $"/api/companies/{targetCompanyId}/locations",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompanyLocations_Delete_WhenLocationMissing_ReturnsNotFound()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Delete Edge Corp");
        await _fixture.SeedEmployeeAsync(companyId, "delete-admin-user", "Delete Admin", "delete-admin@loc.com");

        using var client = _fixture.CreateAuthenticatedClient("delete-admin-user", Roles.Executive);

        var response = await client.DeleteAsync($"/api/companies/locations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompanyLocations_Post_WithInvalidPayload_ReturnsBadRequest()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Validation Corp");
        await _fixture.SeedEmployeeAsync(companyId, "validation-admin-user", "Validation Admin", "validation-admin@loc.com");

        using var client = _fixture.CreateAuthenticatedClient("validation-admin-user", Roles.Executive);

        var invalidPayload = """
                             {
                               "locationName": "",
                               "address": "Main St",
                               "latitude": 30.0444,
                               "longitude": 31.2357
                             }
                             """;

        var response = await client.PostAsync(
            $"/api/companies/{companyId}/locations",
            new StringContent(invalidPayload, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
