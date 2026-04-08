using System.Net;
using System.Text;
using FluentAssertions;
using HrSystemApp.Api.Authorization;
using HrSystemApp.Tests.Integration.Infrastructure;

namespace HrSystemApp.Tests.Integration.Endpoints;

[Collection("Integration")]
public class AuthorizationMatrixEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public AuthorizationMatrixEndpointTests(IntegrationTestFixture fixture)
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

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized(string method, string path, string? jsonBody)
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.Factory.CreateClient();
        var response = await SendAsync(client, method, path, jsonBody);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {path} should require authentication");
    }

    [Theory]
    [MemberData(nameof(RoleRestrictedEndpoints))]
    public async Task RoleRestrictedEndpoints_WithEmployeeRole_ReturnForbidden(string method, string path, string? jsonBody)
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.CreateAuthenticatedClient("matrix-employee", Roles.Employee);
        var response = await SendAsync(client, method, path, jsonBody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {path} should block Employee role");
    }

    [Theory]
    [MemberData(nameof(AnonymousEndpoints))]
    public async Task AnonymousEndpoints_WithoutToken_AreNotUnauthorized(string method, string path, string? jsonBody)
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        using var client = _fixture.Factory.CreateClient();
        var response = await SendAsync(client, method, path, jsonBody);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, $"{method} {path} should allow anonymous access");
    }

    public static IEnumerable<object?[]> ProtectedEndpoints()
    {
        // AuthController (protected actions)
        yield return Case("POST", "/api/auth/logout", """{"refreshToken":"x"}""");
        yield return Case("POST", "/api/auth/revoke", """{"refreshToken":"x"}""");
        yield return Case("POST", "/api/auth/revoke-all", null);
        yield return Case("GET", "/api/auth/tokens", null);
        yield return Case("POST", "/api/auth/change-password", """{"currentPassword":"a","newPassword":"b"}""");
        yield return Case("POST", "/api/auth/update-fcm-token", """{"fcmToken":"x","deviceType":0}""");
        yield return Case("POST", "/api/auth/update-language", """{"language":"en"}""");
        yield return Case("GET", "/api/auth/me", null);

        // AdminManagementController
        yield return Case("PUT", "/api/admin/employees/00000000-0000-0000-0000-000000000001/leave-balances",
            """{"leaveType":0,"year":2026,"totalDays":10}""");
        yield return Case("POST", "/api/admin/initialize-leave-year/2026", null);

        // AttendanceController
        yield return Case("POST", "/api/attendance/clock-in", "{}");
        yield return Case("POST", "/api/attendance/clock-out", "{}");
        yield return Case("GET", "/api/attendance/me?pageNumber=1&pageSize=10", null);
        yield return Case("GET", "/api/attendance?pageNumber=1&pageSize=10", null);
        yield return Case("POST", "/api/attendance/admin/override-clock-out",
            """{"employeeId":"00000000-0000-0000-0000-000000000001","date":"2026-01-01","clockOutUtc":"2026-01-01T17:00:00Z","reason":"x"}""");
        yield return Case("POST", "/api/attendance/admin/override-clock-out/batch",
            """{"items":[{"employeeId":"00000000-0000-0000-0000-000000000001","date":"2026-01-01","clockOutUtc":"2026-01-01T17:00:00Z","reason":"x"}]}""");

        // CompaniesController
        yield return Case("POST", "/api/companies",
            """{"companyName":"Acme","companyLogoUrl":null,"yearlyVacationDays":21,"startTime":"09:00:00","endTime":"17:00:00","graceMinutes":15,"timeZoneId":"UTC"}""");
        yield return Case("GET", "/api/companies?pageNumber=1&pageSize=10", null);
        yield return Case("GET", "/api/companies/00000000-0000-0000-0000-000000000001", null);
        yield return Case("PUT", "/api/companies/00000000-0000-0000-0000-000000000001",
            """{"companyName":"Acme","companyLogoUrl":null,"yearlyVacationDays":21,"startTime":"09:00:00","endTime":"17:00:00","graceMinutes":15,"timeZoneId":"UTC"}""");
        yield return Case("GET", "/api/companies/me", null);
        yield return Case("PUT", "/api/companies/me",
            """{"companyName":"Acme","companyLogoUrl":null,"yearlyVacationDays":21,"startTime":"09:00:00","endTime":"17:00:00","graceMinutes":15,"timeZoneId":"UTC"}""");
        yield return Case("PATCH", "/api/companies/00000000-0000-0000-0000-000000000001/status",
            """{"status":2}""");
        yield return Case("GET", "/api/companies/00000000-0000-0000-0000-000000000001/locations", null);
        yield return Case("POST", "/api/companies/00000000-0000-0000-0000-000000000001/locations",
            """{"locationName":"HQ","address":"Main","latitude":30.0,"longitude":31.0}""");
        yield return Case("DELETE", "/api/companies/locations/00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/companies/hierarchy", null);
        yield return Case("POST", "/api/companies/hierarchy/positions", "[]");

        // ContactAdminController (protected actions only)
        yield return Case("GET", "/api/contactadmin/admin/contact-requests?pageNumber=1&pageSize=10", null);
        yield return Case("POST", "/api/contactadmin/admin/contact-requests/00000000-0000-0000-0000-000000000001/accept", null);
        yield return Case("POST", "/api/contactadmin/admin/contact-requests/00000000-0000-0000-0000-000000000001/reject", null);

        // DepartmentsController
        yield return Case("GET", "/api/departments?companyId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/departments/00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/departments",
            """{"companyId":"00000000-0000-0000-0000-000000000001","name":"Eng","description":"x","vicePresidentId":null,"managerId":null}""");
        yield return Case("PUT", "/api/departments/00000000-0000-0000-0000-000000000001",
            """{"name":"Eng","description":"x","vicePresidentId":null,"managerId":null}""");
        yield return Case("DELETE", "/api/departments/00000000-0000-0000-0000-000000000001", null);

        // EmployeesController
        yield return Case("GET", "/api/employees?page=1&pageSize=5", null);
        yield return Case("GET", "/api/employees/me/profile", null);
        yield return Case("GET", "/api/employees/me/balances", null);
        yield return Case("GET", "/api/employees/00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/employees",
            """{"fullName":"Ali","email":"ali@x.com","phoneNumber":"01000000000","companyId":"00000000-0000-0000-0000-000000000001","role":8}""");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001",
            """{"fullName":"Ali","phoneNumber":"01000000000","address":"x","departmentId":null,"unitId":null,"teamId":null,"managerId":null,"medicalClass":null,"contractEndDate":null}""");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001/assign-team",
            """{"teamId":"00000000-0000-0000-0000-000000000001"}""");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001/deactivate", null);
        yield return Case("POST", "/api/employees/me/profile-update-requests", """{"phoneNumber":"01000000000"}""");
        yield return Case("GET", "/api/employees/me/profile-update-requests?page=1&pageSize=10", null);
        yield return Case("GET", "/api/employees/profile-update-requests?page=1&pageSize=10", null);
        yield return Case("PATCH", "/api/employees/profile-update-requests/00000000-0000-0000-0000-000000000001/handle",
            """{"action":1,"note":"ok"}""");

        // MinioController
        yield return Case("POST", "/api/minio/upload?bucketName=b&objectName=o", null);
        yield return Case("GET", "/api/minio/get-url?bucketName=b&objectName=o", null);
        yield return Case("DELETE", "/api/minio/delete?bucketName=b&objectName=o", null);
        yield return Case("GET", "/api/minio/list-objects?bucketName=b", null);
        yield return Case("GET", "/api/minio/bucket-exists?bucketName=b", null);

        // NotificationsController
        yield return Case("GET", "/api/notifications/me", null);
        yield return Case("PATCH", "/api/notifications/00000000-0000-0000-0000-000000000001/read", null);
        yield return Case("POST", "/api/notifications/send",
            """{"employeeId":"00000000-0000-0000-0000-000000000001","title":"t","message":"m","type":0}""");
        yield return Case("POST", "/api/notifications/broadcast",
            """{"title":"t","message":"m","type":0}""");

        // RequestDefinitionsController (protected actions only)
        yield return Case("POST", "/api/requestdefinitions", "{}");
        yield return Case("PUT", "/api/requestdefinitions/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("DELETE", "/api/requestdefinitions/00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/requestdefinitions", null);

        // RequestsController
        yield return Case("POST", "/api/Employees/requests/me", "{}");
        yield return Case("GET", "/api/Employees/requests/me?pageNumber=1&pageSize=10", null);
        yield return Case("GET", "/api/Employees/requests/me/00000000-0000-0000-0000-000000000001", null);
        yield return Case("PUT", "/api/Employees/requests/me/00000000-0000-0000-0000-000000000001",
            """{"id":"00000000-0000-0000-0000-000000000001"}""");
        yield return Case("DELETE", "/api/Employees/requests/me/00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/Employees/requests/approvals/pending?pageNumber=1&pageSize=10", null);
        yield return Case("POST", "/api/Employees/requests/approvals/00000000-0000-0000-0000-000000000001/approve",
            """{"comment":"ok"}""");
        yield return Case("POST", "/api/Employees/requests/approvals/00000000-0000-0000-0000-000000000001/reject",
            """{"comment":"no"}""");
        yield return Case("GET", "/api/Employees/requests/admin/company-wide?pageNumber=1&pageSize=10", null);

        // TeamsController
        yield return Case("GET", "/api/teams?unitId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/teams/00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/teams",
            """{"unitId":"00000000-0000-0000-0000-000000000001","name":"API","description":"x","teamLeaderId":null}""");
        yield return Case("PUT", "/api/teams/00000000-0000-0000-0000-000000000001",
            """{"name":"API","description":"x","teamLeaderId":null}""");
        yield return Case("DELETE", "/api/teams/00000000-0000-0000-0000-000000000001", null);

        // UnitsController
        yield return Case("GET", "/api/units?departmentId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/units/00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/units",
            """{"departmentId":"00000000-0000-0000-0000-000000000001","name":"Platform","description":"x","unitLeaderId":null}""");
        yield return Case("PUT", "/api/units/00000000-0000-0000-0000-000000000001",
            """{"name":"Platform","description":"x","unitLeaderId":null}""");
        yield return Case("DELETE", "/api/units/00000000-0000-0000-0000-000000000001", null);
    }

    public static IEnumerable<object?[]> RoleRestrictedEndpoints()
    {
        // Employee role should be forbidden for these role-gated endpoints.
        yield return Case("POST", "/api/companies", "{}");
        yield return Case("GET", "/api/companies", null);
        yield return Case("GET", "/api/companies/00000000-0000-0000-0000-000000000001", null);
        yield return Case("PUT", "/api/companies/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("PUT", "/api/companies/me", "{}");
        yield return Case("PATCH", "/api/companies/00000000-0000-0000-0000-000000000001/status", """{"status":1}""");
        yield return Case("GET", "/api/companies/00000000-0000-0000-0000-000000000001/locations", null);
        yield return Case("POST", "/api/companies/00000000-0000-0000-0000-000000000001/locations", "{}");
        yield return Case("DELETE", "/api/companies/locations/00000000-0000-0000-0000-000000000001", null);
        yield return Case("GET", "/api/companies/hierarchy", null);
        yield return Case("POST", "/api/companies/hierarchy/positions", "[]");

        yield return Case("GET", "/api/departments?companyId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/departments", "{}");
        yield return Case("PUT", "/api/departments/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("DELETE", "/api/departments/00000000-0000-0000-0000-000000000001", null);

        yield return Case("GET", "/api/employees?page=1&pageSize=5", null);
        yield return Case("POST", "/api/employees", "{}");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001/assign-team", "{}");
        yield return Case("PUT", "/api/employees/00000000-0000-0000-0000-000000000001/deactivate", null);
        yield return Case("GET", "/api/employees/profile-update-requests", null);
        yield return Case("PATCH", "/api/employees/profile-update-requests/00000000-0000-0000-0000-000000000001/handle", "{}");

        yield return Case("GET", "/api/attendance", null);
        yield return Case("POST", "/api/attendance/admin/override-clock-out", "{}");
        yield return Case("POST", "/api/attendance/admin/override-clock-out/batch", """{"items":[]}""");

        yield return Case("GET", "/api/contactadmin/admin/contact-requests", null);
        yield return Case("POST", "/api/contactadmin/admin/contact-requests/00000000-0000-0000-0000-000000000001/accept", null);
        yield return Case("POST", "/api/contactadmin/admin/contact-requests/00000000-0000-0000-0000-000000000001/reject", null);

        yield return Case("POST", "/api/minio/upload?bucketName=b&objectName=o", null);
        yield return Case("GET", "/api/minio/get-url?bucketName=b&objectName=o", null);
        yield return Case("DELETE", "/api/minio/delete?bucketName=b&objectName=o", null);

        yield return Case("POST", "/api/notifications/send", "{}");
        yield return Case("POST", "/api/notifications/broadcast", "{}");

        yield return Case("GET", "/api/teams?unitId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/teams", "{}");
        yield return Case("PUT", "/api/teams/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("DELETE", "/api/teams/00000000-0000-0000-0000-000000000001", null);

        yield return Case("GET", "/api/units?departmentId=00000000-0000-0000-0000-000000000001", null);
        yield return Case("POST", "/api/units", "{}");
        yield return Case("PUT", "/api/units/00000000-0000-0000-0000-000000000001", "{}");
        yield return Case("DELETE", "/api/units/00000000-0000-0000-0000-000000000001", null);
    }

    public static IEnumerable<object?[]> AnonymousEndpoints()
    {
        yield return Case("GET", "/api/health", null);
        // Login may validly return 401 for bad credentials; the dedicated auth endpoint tests should cover that.
        yield return Case("POST", "/api/auth/refresh", """{"refreshToken":"bad"}""");
        yield return Case("POST", "/api/auth/force-change-password",
            """{"userId":"u","currentPassword":"x","newPassword":"y"}""");
        yield return Case("POST", "/api/auth/forgot-password", """{"email":"x@y.com","channel":0}""");
        yield return Case("POST", "/api/auth/verify-otp", """{"email":"x@y.com","otp":"123456"}""");
        yield return Case("POST", "/api/auth/reset-password", """{"email":"x@y.com","otp":"123456","newPassword":"x"}""");
        yield return Case("POST", "/api/contactadmin", """{"name":"n","email":"x@y.com","phone":"010","companyName":"Acme"}""");
        yield return Case("GET", "/api/requestdefinitions/types", null);
        yield return Case("GET", "/api/requestdefinitions/schemas", null);
    }

    private static object?[] Case(string method, string path, string? jsonBody)
    {
        return new object?[] { method, path, jsonBody };
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path, string? jsonBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (jsonBody != null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }
}
