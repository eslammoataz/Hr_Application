using HrSystemApp.Application.Common.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using HrSystemApp.Api;
using HrSystemApp.Application;
using Serilog;
using HrSystemApp.Api.Middleware;
using HrSystemApp.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using Elastic.Serilog.Sinks;
using Elastic.Ingest.Elasticsearch;
using HrSystemApp.Infrastructure.Services;
using Microsoft.AspNetCore.Localization;
using HrSystemApp.Api.Localization;

var builder = WebApplication.CreateBuilder(args);

// ── Bootstrap logger ──────────────────────────────────────────────────────────
// Minimal console sink for startup/host errors emitted before the DI container
// is available. Replaced by the full logger once the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// Enable Serilog SelfLog so sink errors appear in the console.
Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine($"Serilog Error: {msg}"));

// ── Full logger ───────────────────────────────────────────────────────────────
// UseSerilog callback runs after the DI container is built, so services
// (including RequestContextEnricher) are available via ReadFrom.Services(services).
builder.Host.UseSerilog((ctx, services, config) =>
{
    config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)   // picks up ILogEventEnricher (RequestContextEnricher) from DI
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "HrSystemApp");

    var seqEnabled = ctx.Configuration.GetValue<bool>("SeqSettings:Enabled");
    if (seqEnabled)
    {
        var seqUrl = ctx.Configuration.GetValue<string>("SeqSettings:ServerUrl") ?? "http://localhost:5341";
        config.WriteTo.Seq(seqUrl);
        Log.Information("🚀 Seq Logging is ENABLED at {SeqUrl}", seqUrl);
    }
    else
    {
        Log.Warning("⚠️ Seq Logging is DISABLED.");
    }

    var elasticEnabled = ctx.Configuration.GetValue<bool>("ElasticSettings:Enabled");
    if (elasticEnabled)
    {
        var nodeUri = ctx.Configuration["ElasticSettings:NodeUri"] ?? "http://localhost:9200";
        var prefix = ctx.Configuration["ElasticSettings:IndexPrefix"] ?? "hrsystemapp";
        var env = ctx.Configuration["ElasticSettings:Environment"] ?? "production";
        var indexFormat = $"{prefix}-{env}-{{0:yyyy.MM.dd}}";

        config.WriteTo.Elasticsearch(new[] { new Uri(nodeUri) }, opts =>
        {
            opts.DataStream = new Elastic.Ingest.Elasticsearch.DataStreams.DataStreamName("logs", prefix, env);
        });

        Log.Information("🚀 ElasticSearch Logging is ENABLED at {NodeUri}", nodeUri);
    }
});

// Add Api layer services
builder.Services.AddApi(builder.Configuration);

// Add Application layer services
builder.Services.AddApplication();
builder.Services.Configure<LoggingOptions>(builder.Configuration.GetSection("LoggingOptions"));

// Localization — supports Accept-Language header (en, ar)
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "ar" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);

    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new AcceptLanguageHeaderRequestCultureProvider(),
        new UserLanguageRequestCultureProvider()
    };
});

// Add Infrastructure layer services
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHangfire(config =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    config.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
        {
            options.UseNpgsqlConnection(connectionString);
        });
});
builder.Services.AddHangfireServer();


// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HrSystemApp API",
        Version = "v1",
        Description = "API for HrSystemApp application"
    });

    // JWT Authentication in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {your_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS (configure as needed)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// Database Initialization (Migrations & Seeding)
// ══════════════════════════════════════════════════════════════
var applyMigrations = app.Configuration.GetValue("ApplyMigrationsOnStartup", true);

Log.Information("🚀 Starting database initialization...");
try
{
    await app.Services.InitialiseDatabaseAsync(applyMigrations);
    Log.Information("✅ Database initialization completed successfully.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Database initialization failed.");
    throw;
}

// Swagger UI (available in all environments for now)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "HrSystemApp API v1");
    options.RoutePrefix = "swagger";
});

// ── Middleware pipeline ───────────────────────────────────────────────────────

// Localization — must be first to set CultureInfo.CurrentUICulture per request.
app.UseRequestLocalization();

// CorrelationId: assigns/propagates X-Correlation-ID and pushes it to LogContext.
app.UseMiddleware<CorrelationIdMiddleware>();

// Request/response logging (skips /health and /swagger paths).
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Global exception handler — wraps all downstream middleware.
app.UseMiddleware<ExceptionMiddleware>();

// CORS
app.UseCors("AllowAll");

// Routing must run before auth and endpoint mapping.
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// NOTE: LoggingScopeMiddleware has been removed.
// User context (UserId, Email, AppEnvironment) is now enriched by RequestContextEnricher,
// a Serilog ILogEventEnricher that reads IHttpContextAccessor lazily at log-emit time.
// This correctly handles logs emitted before, during, and after the middleware pipeline.

// Map controllers
app.MapControllers();
app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<AttendanceRecurringJobs>(
    "attendance-reminder-job",
    job => job.RunReminderJob(),
    Cron.Daily);

RecurringJob.AddOrUpdate<AttendanceRecurringJobs>(
    "attendance-auto-clockout-job",
    job => job.RunAutoClockOutJob(),
    Cron.Daily);

app.Run();

public partial class Program { }
