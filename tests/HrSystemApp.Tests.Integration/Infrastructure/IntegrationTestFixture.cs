using System.Net.Http.Headers;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace HrSystemApp.Tests.Integration.Infrastructure;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private Respawner? _respawner;

    public bool DockerUnavailable { get; private set; }
    public string? DockerUnavailableReason { get; private set; }
    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _postgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("hrsystemapp_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgresContainer.StartAsync();

            Factory = new CustomWebApplicationFactory(_postgresContainer.GetConnectionString());
            using var client = Factory.CreateClient();

            await InitializeRespawnerAsync();
        }
        catch (Exception ex)
        {
            DockerUnavailable = true;
            DockerUnavailableReason = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    public HttpClient CreateAuthenticatedClient(string userId, string role, Guid? companyId = null)
    {
        EnsureDockerAvailable();

        var configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var token = JwtTokenFactory.CreateToken(configuration, userId, role, companyId);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task ResetDatabaseAsync()
    {
        if (DockerUnavailable)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_postgresContainer!.GetConnectionString());
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public async Task<Guid> SeedCompanyAsync(string name, CompanyStatus status = CompanyStatus.Active)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var company = new Company
        {
            CompanyName = name,
            YearlyVacationDays = 21,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            GraceMinutes = 15,
            TimeZoneId = "UTC",
            Status = status
        };

        await context.Companies.AddAsync(company);
        await context.SaveChangesAsync();
        return company.Id;
    }

    public async Task<Guid> SeedEmployeeAsync(
        Guid companyId,
        string userId,
        string fullName,
        string email,
        EmploymentStatus employmentStatus = EmploymentStatus.Active)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var normalizedEmail = email.ToUpperInvariant();

        var existingUser = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (existingUser is null)
        {
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                Name = fullName,
                EmailConfirmed = true
            };

            await context.Users.AddAsync(user);
        }

        var employee = new Employee
        {
            CompanyId = companyId,
            UserId = userId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}"[..10],
            FullName = fullName,
            Email = email,
            PhoneNumber = "01000000000",
            EmploymentStatus = employmentStatus
        };

        await context.Employees.AddAsync(employee);
        await context.SaveChangesAsync();

        return employee.Id;
    }

    public async Task<Guid> SeedEmployeeWithOrgAsync(
        Guid companyId,
        string userId,
        string fullName,
        string email,
        Guid? departmentId = null,
        Guid? unitId = null,
        Guid? teamId = null)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var normalizedEmail = email.ToUpperInvariant();

        var existingUser = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (existingUser is null)
        {
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                Name = fullName,
                EmailConfirmed = true
            };

            await context.Users.AddAsync(user);
        }

        var employee = new Employee
        {
            CompanyId = companyId,
            UserId = userId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}"[..10],
            FullName = fullName,
            Email = email,
            PhoneNumber = "01000000000",
            EmploymentStatus = EmploymentStatus.Active,
            DepartmentId = departmentId,
            UnitId = unitId,
            TeamId = teamId
        };

        await context.Employees.AddAsync(employee);
        await context.SaveChangesAsync();

        return employee.Id;
    }

    public async Task<Guid> SeedDepartmentAsync(
        Guid companyId,
        string name,
        Guid? vicePresidentId = null,
        Guid? managerId = null)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var department = new Department
        {
            CompanyId = companyId,
            Name = name,
            VicePresidentId = vicePresidentId,
            ManagerId = managerId
        };

        await context.Departments.AddAsync(department);
        await context.SaveChangesAsync();

        return department.Id;
    }

    public async Task<Guid> SeedUnitAsync(Guid departmentId, string name, Guid? unitLeaderId = null)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = new Unit
        {
            DepartmentId = departmentId,
            Name = name,
            UnitLeaderId = unitLeaderId
        };

        await context.Units.AddAsync(unit);
        await context.SaveChangesAsync();

        return unit.Id;
    }

    public async Task<Guid> SeedTeamAsync(Guid unitId, string name, Guid? teamLeaderId = null)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var team = new Team
        {
            UnitId = unitId,
            Name = name,
            TeamLeaderId = teamLeaderId
        };

        await context.Teams.AddAsync(team);
        await context.SaveChangesAsync();

        return team.Id;
    }

    public async Task SeedHierarchyPositionsAsync(Guid companyId, params (UserRole Role, string Title, int SortOrder)[] positions)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await context.CompanyHierarchyPositions
            .Where(x => x.CompanyId == companyId)
            .ToListAsync();
        if (existing.Count > 0)
        {
            context.CompanyHierarchyPositions.RemoveRange(existing);
        }

        foreach (var position in positions)
        {
            await context.CompanyHierarchyPositions.AddAsync(new CompanyHierarchyPosition
            {
                CompanyId = companyId,
                Role = position.Role,
                PositionTitle = position.Title,
                SortOrder = position.SortOrder
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task AssignRoleToUserAsync(string userId, string roleName)
    {
        EnsureDockerAvailable();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == roleName);
        if (role is null)
        {
            role = new IdentityRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };

            await context.Roles.AddAsync(role);
        }

        var mappingExists = await context.UserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == role.Id);
        if (!mappingExists)
        {
            await context.UserRoles.AddAsync(new IdentityUserRole<string>
            {
                UserId = userId,
                RoleId = role.Id
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task InitializeRespawnerAsync()
    {
        await using var connection = new NpgsqlConnection(_postgresContainer!.GetConnectionString());
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Table[] { "__EFMigrationsHistory" }
        });
    }

    private void EnsureDockerAvailable()
    {
        if (!DockerUnavailable)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Docker is unavailable in the current environment. {DockerUnavailableReason}");
    }
}
