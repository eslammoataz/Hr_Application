using System.Diagnostics;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Serilog.Context;

namespace HrSystemApp.Api.Middleware;

public class LoggingScopeMiddleware
{
    private readonly RequestDelegate _next;

    public LoggingScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        var userId = currentUser.UserId ?? "Anonymous";
        var email = currentUser.Email;
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var requestPath = context.Request.Path.Value;
        var httpMethod = context.Request.Method;
        var appEnvironment = context.RequestServices.GetService<IWebHostEnvironment>()?.EnvironmentName ?? "Unknown";

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("Email", email))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("RequestPath", requestPath))
        using (LogContext.PushProperty("HttpMethod", httpMethod))
        using (LogContext.PushProperty("AppEnvironment", appEnvironment))
        {
            await _next(context);
        }
    }
}
