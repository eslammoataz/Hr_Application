using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using HrSystemApp.Api;
using HrSystemApp.Application;
using HrSystemApp.Infrastructure.Data;
using Serilog;
using HrSystemApp.Api.Middleware;
using HrSystemApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration);

// Add Seq if enabled in settings
var seqEnabled = builder.Configuration.GetValue<bool>("SeqSettings:Enabled");
if (seqEnabled)
{
    var seqUrl = builder.Configuration.GetValue<string>("SeqSettings:ServerUrl") ?? "http://localhost:5341";
    loggerConfiguration.WriteTo.Seq(seqUrl);
}

Log.Logger = loggerConfiguration.CreateLogger();

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
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// Configure the HTTP request pipeline
// ══════════════════════════════════════════════════════════════

// Apply migrations automatically in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
}

// Seed SuperAdmin on every startup (idempotent – skips if already exists)
using (var scope = app.Services.CreateScope())
{
    await SeedData.InitializeAsync(scope.ServiceProvider);
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
app.UseMiddleware<HrSystemApp.Api.Middleware.ExceptionMiddleware>();

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
