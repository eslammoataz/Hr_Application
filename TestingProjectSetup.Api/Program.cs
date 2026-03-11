using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TestingProjectSetup.Api;
using TestingProjectSetup.Application;
using TestingProjectSetup.Infrastructure;
using TestingProjectSetup.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

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
        Title = "TestingProjectSetup API",
        Version = "v1",
        Description = "API for TestingProjectSetup application"
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

// Swagger UI (available in all environments for now)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TestingProjectSetup API v1");
    options.RoutePrefix = "swagger";
});

// Global exception handling (early in pipeline to catch all downstream errors)
app.UseMiddleware<TestingProjectSetup.Api.Middleware.ExceptionMiddleware>();

// Routing must run before CORS, Auth, and endpoints
app.UseRouting();

// CORS
app.UseCors("AllowAll");

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
