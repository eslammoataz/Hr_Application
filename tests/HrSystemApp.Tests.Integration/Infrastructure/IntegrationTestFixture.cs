using System.Net.Http.Headers;
using HrSystemApp.Domain.Enums;
using HrSystemApp.Domain.Models;
using HrSystemApp.Infrastructure.Data;
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

    public HttpClient CreateAuthenticatedClient(string userId, string role)
    {
        EnsureDockerAvailable();

        var configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var token = JwtTokenFactory.CreateToken(configuration, userId, role);

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

    public async Task<Guid> SeedEmployeeAsync(Guid companyId, string userId, string fullName, string email)
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
            EmploymentStatus = EmploymentStatus.Active
        };

        await context.Employees.AddAsync(employee);
        await context.SaveChangesAsync();

        return employee.Id;
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
