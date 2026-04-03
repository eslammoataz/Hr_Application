using Microsoft.OpenApi.Models;
using HrSystemApp.Api;
using HrSystemApp.Application;
using Serilog;
using HrSystemApp.Api.Middleware;
using HrSystemApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration);

// Add Seq if enabled in settings
var seqEnabled = builder.Configuration.GetValue<bool>("SeqSettings:Enabled");
var seqUrl = builder.Configuration.GetValue<string>("SeqSettings:ServerUrl") ?? "http://localhost:5341";

if (seqEnabled)
{
    loggerConfiguration.WriteTo.Seq(seqUrl);
}

Log.Logger = loggerConfiguration.CreateLogger();

// Verify Seq logging at startup
if (seqEnabled)
{
    Log.Information("🚀 Seq Logging is ENABLED at {SeqUrl}", seqUrl);
}
else
{
    Log.Warning("⚠️ Seq Logging is DISABLED.");
}

// Enable Serilog SelfLog to output sink errors to the console
Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine($"Serilog Error: {msg}"));

builder.Host.UseSerilog();

// Add Api layer services 
builder.Services.AddApi(builder.Configuration);

// Add Application layer services 
builder.Services.AddApplication();

// Add Infrastructure layer services
builder.Services.AddInfrastructure(builder.Configuration);


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

// Custom Logging Middlewares
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Global exception handling (early in pipeline to catch all downstream errors)
app.UseMiddleware<ExceptionMiddleware>();

// CORS
app.UseCors("AllowAll");

// Routing must run before CORS, Auth, and endpoints
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Run the application
app.Run();
