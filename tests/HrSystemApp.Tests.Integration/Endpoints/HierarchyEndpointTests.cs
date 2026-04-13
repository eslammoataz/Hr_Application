using System.Net;
using System.Text.Json;
using FluentAssertions;
using HrSystemApp.Api.Authorization;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace HrSystemApp.Tests.Integration.Endpoints;

[Collection("Integration")]
public class HierarchyEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public HierarchyEndpointTests(IntegrationTestFixture fixture)
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

    [Fact(Skip = "Disabled")]
    public async Task GetHierarchy_ReturnsRoleOrderedTree_WithDepartmentsUnitsTeamsAndEmployees()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Hierarchy Corp");

        await _fixture.SeedHierarchyPositionsAsync(
            companyId,
            (UserRole.CEO, "CEO", 1),
            (UserRole.VicePresident, "VP", 2),
            (UserRole.DepartmentManager, "Department Manager", 3),
            (UserRole.UnitLeader, "Unit Leader", 4),
            (UserRole.TeamLeader, "Team Leader", 5),
            (UserRole.Employee, "Employee", 6));

        await _fixture.SeedEmployeeWithOrgAsync(companyId, "viewer-user", "Viewer User", "viewer@corp.com");
        await _fixture.AssignRoleToUserAsync("viewer-user", Roles.HR);

        var ceoId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "ceo-user", "CEO User", "ceo@corp.com");
        await _fixture.AssignRoleToUserAsync("ceo-user", Roles.CEO);

        var vp1Id = await _fixture.SeedEmployeeWithOrgAsync(companyId, "vp1-user", "VP One", "vp1@corp.com");
        var vp2Id = await _fixture.SeedEmployeeWithOrgAsync(companyId, "vp2-user", "VP Two", "vp2@corp.com");

        var mgr1Id = await _fixture.SeedEmployeeWithOrgAsync(companyId, "mgr1-user", "Manager One", "mgr1@corp.com");
        var mgr2Id = await _fixture.SeedEmployeeWithOrgAsync(companyId, "mgr2-user", "Manager Two", "mgr2@corp.com");

        var dep1Id = await _fixture.SeedDepartmentAsync(companyId, "Engineering", vp1Id, mgr1Id);
        var dep2Id = await _fixture.SeedDepartmentAsync(companyId, "Operations", vp2Id, mgr2Id);

        var unit1LeaderId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "ul1-user", "Unit Leader One", "ul1@corp.com");
        var unit2LeaderId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "ul2-user", "Unit Leader Two", "ul2@corp.com");

        var unit1Id = await _fixture.SeedUnitAsync(dep1Id, "Platform", unit1LeaderId);
        var unit2Id = await _fixture.SeedUnitAsync(dep2Id, "Logistics", unit2LeaderId);

        var team1LeaderId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "tl1-user", "Team Leader One", "tl1@corp.com");
        var team2LeaderId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "tl2-user", "Team Leader Two", "tl2@corp.com");

        var team1Id = await _fixture.SeedTeamAsync(unit1Id, "Backend", team1LeaderId);
        var team2Id = await _fixture.SeedTeamAsync(unit2Id, "Dispatch", team2LeaderId);

        await _fixture.SeedEmployeeWithOrgAsync(companyId, "unit-emp-user", "Unit Direct Employee", "unit@corp.com", dep1Id, unit1Id);
        await _fixture.SeedEmployeeWithOrgAsync(companyId, "team-emp-a", "Team Member A", "a@corp.com", dep1Id, unit1Id, team1Id);
        await _fixture.SeedEmployeeWithOrgAsync(companyId, "team-emp-b", "Team Member B", "b@corp.com", dep2Id, unit2Id, team2Id);

        // Assign Roles to actors (CRITICAL for new role-based DTO mapping)
        await _fixture.AssignRoleToUserAsync("vp1-user", Roles.VicePresident);
        await _fixture.AssignRoleToUserAsync("vp2-user", Roles.VicePresident);
        await _fixture.AssignRoleToUserAsync("mgr1-user", Roles.DepartmentManager);
        await _fixture.AssignRoleToUserAsync("mgr2-user", Roles.DepartmentManager);
        await _fixture.AssignRoleToUserAsync("ul1-user", Roles.UnitLeader);
        await _fixture.AssignRoleToUserAsync("ul2-user", Roles.UnitLeader);
        await _fixture.AssignRoleToUserAsync("tl1-user", Roles.TeamLeader);
        await _fixture.AssignRoleToUserAsync("tl2-user", Roles.TeamLeader);
        await _fixture.AssignRoleToUserAsync("unit-emp-user", Roles.Employee);
        await _fixture.AssignRoleToUserAsync("team-emp-a", Roles.Employee);
        await _fixture.AssignRoleToUserAsync("team-emp-b", Roles.Employee);

        // Manually link managers to build the reporting chain
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HrSystemApp.Infrastructure.Data.ApplicationDbContext>();
            var ceo = await context.Employees.FirstAsync(e => e.FullName == "CEO User");
            var vp1 = await context.Employees.FirstAsync(e => e.FullName == "VP One");
            var vp2 = await context.Employees.FirstAsync(e => e.FullName == "VP Two");
            var mgr1 = await context.Employees.FirstAsync(e => e.FullName == "Manager One");
            var mgr2 = await context.Employees.FirstAsync(e => e.FullName == "Manager Two");
            var ul1 = await context.Employees.FirstAsync(e => e.FullName == "Unit Leader One");
            var ul2 = await context.Employees.FirstAsync(e => e.FullName == "Unit Leader Two");
            var tl1 = await context.Employees.FirstAsync(e => e.FullName == "Team Leader One");
            var tl2 = await context.Employees.FirstAsync(e => e.FullName == "Team Leader Two");
            var empUnit = await context.Employees.FirstAsync(e => e.FullName == "Unit Direct Employee");
            var empA = await context.Employees.FirstAsync(e => e.FullName == "Team Member A");
            var empB = await context.Employees.FirstAsync(e => e.FullName == "Team Member B");
            var viewer = await context.Employees.FirstAsync(e => e.FullName == "Viewer User");

            vp1.ManagerId = ceo.Id;
            vp2.ManagerId = ceo.Id;
            viewer.ManagerId = ceo.Id; // Keep viewer under CEO

            mgr1.ManagerId = vp1.Id;
            mgr2.ManagerId = vp2.Id;

            ul1.ManagerId = mgr1.Id;
            ul2.ManagerId = mgr2.Id;

            tl1.ManagerId = ul1.Id;
            tl2.ManagerId = ul2.Id;

            empUnit.ManagerId = ul1.Id;
            empA.ManagerId = tl1.Id;
            empB.ManagerId = tl2.Id;

            await context.SaveChangesAsync();
        }

        using var client = _fixture.CreateAuthenticatedClient("viewer-user", Roles.HR);

        // 1. Get Roots
        var response = await client.GetAsync("/api/companies/hierarchy");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Should return the CEO as root
        nodes.Should().HaveCount(1);
        var ceoNode = nodes[0];
        ceoNode.GetProperty("nodeType").GetString().Should().Be("Employee");
        ceoNode.GetProperty("name").GetString().Should().Be("CEO User");
        ceoNode.GetProperty("hasChildren").GetBoolean().Should().BeTrue();

        var ceoIdStr = ceoNode.GetProperty("id").GetString();

        // 2. Expand CEO
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={ceoIdStr}&parentType=Employee");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        json = await response.Content.ReadAsStringAsync();
        using var ceoDoc = JsonDocument.Parse(json);
        nodes = ceoDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Should return 2 VPs
        nodes.Where(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("role").GetString() == "VicePresident")
            .Should().HaveCount(2);

        var vpOne = nodes.First(x => x.GetProperty("name").GetString() == "VP One");
        var vpOneIdStr = vpOne.GetProperty("id").GetString();

        // 3. Expand VP One
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={vpOneIdStr}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var vpOneDoc = JsonDocument.Parse(json);
        nodes = vpOneDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // VP One should return the Department they lead (Engineering)
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Department" && x.GetProperty("name").GetString() == "Engineering")
            .Should().BeTrue();

        // IMPORTANT: The Manager should NOT appear here under the VP anymore. 
        // They will appear when the Department node is expanded.
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Manager One")
            .Should().BeFalse();

        var engDept = nodes.First(x => x.GetProperty("nodeType").GetString() == "Department");
        var engDeptIdStr = engDept.GetProperty("id").GetString();

        // 4. Expand Engineering Department
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={engDeptIdStr}&parentType=Department");
        json = await response.Content.ReadAsStringAsync();
        using var engDoc = JsonDocument.Parse(json);
        nodes = engDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Engineering Department expands ONLY to its Manager
        nodes.Should().HaveCount(1);
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Manager One")
            .Should().BeTrue();

        var managerOne = nodes.First();
        var managerOneIdStr = managerOne.GetProperty("id").GetString();

        // 5. Expand Manager One
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={managerOneIdStr}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var mgrDoc = JsonDocument.Parse(json);
        nodes = mgrDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Manager One unboxes the Units and direct employees
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Unit" && x.GetProperty("name").GetString() == "Platform")
            .Should().BeTrue();

        var platformUnit = nodes.First(x => x.GetProperty("nodeType").GetString() == "Unit");
        var platformUnitIdStr = platformUnit.GetProperty("id").GetString();

        // 6. Expand Platform Unit
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={platformUnitIdStr}&parentType=Unit");
        json = await response.Content.ReadAsStringAsync();
        using var platformDoc = JsonDocument.Parse(json);
        nodes = platformDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Unit expands ONLY to its Leader
        nodes.Should().HaveCount(1);
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Unit Leader One")
            .Should().BeTrue();

        var unitLeaderOne = nodes.First();
        var unitLeaderOneIdStr = unitLeaderOne.GetProperty("id").GetString();

        // 7. Expand Unit Leader One
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={unitLeaderOneIdStr}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var leadDoc = JsonDocument.Parse(json);
        nodes = leadDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Unit Leader One unboxes the Teams and Unit Direct Employees
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Team" && x.GetProperty("name").GetString() == "Platform")
            .Should().BeFalse(); // Unit doesn't reappear
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Team" && x.GetProperty("name").GetString() == "Backend")
            .Should().BeTrue();
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Unit Direct Employee")
            .Should().BeTrue();

        var backendTeam = nodes.First(x => x.GetProperty("nodeType").GetString() == "Team");
        var backendTeamIdStr = backendTeam.GetProperty("id").GetString();

        // 8. Expand Backend Team
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={backendTeamIdStr}&parentType=Team");
        json = await response.Content.ReadAsStringAsync();
        using var teamDoc = JsonDocument.Parse(json);
        nodes = teamDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Team expands ONLY to Team Leader
        nodes.Should().HaveCount(1);
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Team Leader One")
            .Should().BeTrue();

        var teamLeaderOne = nodes.First();
        var teamLeaderOneIdStr = teamLeaderOne.GetProperty("id").GetString();

        // 9. Expand Team Leader
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={teamLeaderOneIdStr}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var tlDoc = JsonDocument.Parse(json);
        nodes = tlDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        // Team Leader unboxes the team members
        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Employee" && x.GetProperty("name").GetString() == "Team Member A")
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetHierarchy_PreservesConfiguredEmptyLevel_WhenDepartmentManagerMissing()
    {
        if (_fixture.DockerUnavailable)
        {
            return;
        }

        var companyId = await _fixture.SeedCompanyAsync("Hierarchy Empty Levels Corp");

        await _fixture.SeedHierarchyPositionsAsync(
            companyId,
            (UserRole.CEO, "CEO", 1),
            (UserRole.VicePresident, "VP", 2),
            (UserRole.DepartmentManager, "Department Manager", 3),
            (UserRole.UnitLeader, "Unit Leader", 4));

        await _fixture.SeedEmployeeWithOrgAsync(companyId, "viewer-empty", "Viewer Empty", "viewer.empty@corp.com");
        await _fixture.AssignRoleToUserAsync("viewer-empty", Roles.HR);

        await _fixture.SeedEmployeeWithOrgAsync(companyId, "ceo-empty", "CEO Empty", "ceo.empty@corp.com");
        await _fixture.AssignRoleToUserAsync("ceo-empty", Roles.CEO);

        var vpId = await _fixture.SeedEmployeeWithOrgAsync(companyId, "vp-empty", "VP Empty", "vp.empty@corp.com");
        await _fixture.AssignRoleToUserAsync("vp-empty", Roles.VicePresident);
        await _fixture.SeedDepartmentAsync(companyId, "No Manager Department", vpId, null);

        // Link CEO to VP
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HrSystemApp.Infrastructure.Data.ApplicationDbContext>();
            var ceo = await context.Employees.FirstAsync(e => e.FullName == "CEO Empty");
            var vp = await context.Employees.FirstAsync(e => e.FullName == "VP Empty");
            vp.ManagerId = ceo.Id;
            await context.SaveChangesAsync();
        }

        using var client = _fixture.CreateAuthenticatedClient("viewer-empty", Roles.HR);

        // Root (CEO)
        var response = await client.GetAsync("/api/companies/hierarchy");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ceoId = doc.RootElement.GetProperty("data").GetProperty("nodes")[0].GetProperty("id").GetString();

        // VP
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={ceoId}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var vpDoc = JsonDocument.Parse(json);
        var vpIdStr = vpDoc.RootElement.GetProperty("data").GetProperty("nodes")[0].GetProperty("id").GetString();

        // Expand VP to see the department even if it has no manager
        response = await client.GetAsync($"/api/companies/hierarchy?parentId={vpIdStr}&parentType=Employee");
        json = await response.Content.ReadAsStringAsync();
        using var deptDoc = JsonDocument.Parse(json);
        var nodes = deptDoc.RootElement.GetProperty("data").GetProperty("nodes").EnumerateArray().ToList();

        nodes.Any(x => x.GetProperty("nodeType").GetString() == "Department" && x.GetProperty("name").GetString() == "No Manager Department")
            .Should().BeTrue();
    }
}
