# Localization Implementation Plan — HrSystemApp

## How it works in this codebase

Every `Error` in your system already has a stable `Code` (e.g. `"Auth.InvalidCredentials"`).
That code is the localization key. At response time you look up `localizer["Auth.InvalidCredentials"]`
in a `.resx` file matching the request language, and replace the English message before serializing
to JSON. The domain layer never changes — only the response layer swaps the message.

Language is determined by the `Accept-Language` HTTP header the client sends:
- `Accept-Language: ar` → Arabic
- `Accept-Language: en` or nothing → English (default)

ASP.NET's `UseRequestLocalization()` middleware reads that header and sets `CultureInfo.CurrentUICulture`
automatically. `IStringLocalizer<T>` uses that culture to pick the right `.resx` file.

---

## Two response paths that must both be localized

```
Handler returns Result.Failure(error)
    └─► BaseApiController.HandleResult()
            └─► HandleError(error)          ← PATH 1: all business logic failures

Exception thrown (validation, unhandled)
    └─► ExceptionMiddleware.HandleExceptionAsync()  ← PATH 2: exceptions
```

Both paths currently serialize `error.Message` directly. Both must call `IErrorLocalizer.Localize(error)`
before building the `ApiResponse`.

---

## Phase 1 — Domain Errors (all business logic)

### Step 1 — Add NuGet package to Application project

`HrSystemApp.Application/HrSystemApp.Application.csproj` — add inside the existing `<ItemGroup>`:

```xml
<PackageReference Include="Microsoft.Extensions.Localization.Abstractions" Version="8.0.0" />
```

---

### Step 2 — Create the IErrorLocalizer interface

**New file: `HrSystemApp.Application/Resources/IErrorLocalizer.cs`**

```csharp
namespace HrSystemApp.Application.Resources;

/// <summary>
/// Translates an Error's message into the current request culture.
/// Uses Error.Code as the lookup key in the resource files.
/// If no translation exists for a key, falls back to the English Error.Message.
/// </summary>
public interface IErrorLocalizer
{
    /// <summary>
    /// Returns a copy of the error with its Message replaced by the localized version.
    /// The Code is always preserved unchanged.
    /// </summary>
    Error Localize(Error error);
}
```

> **Important:** This file lives in `HrSystemApp.Application/Resources/` with namespace
> `HrSystemApp.Application.Resources`. This is intentional — see the note at Step 3 about why.

---

### Step 3 — Create the marker class for IStringLocalizer<T>

These are empty classes whose sole purpose is to be the type parameter for `IStringLocalizer<T>`.
The generic type parameter tells .NET which `.resx` file to load.

**New file: `HrSystemApp.Application/Resources/ErrorMessages.cs`**

```csharp
namespace HrSystemApp.Application.Resources;

/// <summary>
/// Marker class for IStringLocalizer&lt;ErrorMessages&gt;.
/// The corresponding resource files are:
///   Resources/ErrorMessages.resx        (English — default)
///   Resources/ErrorMessages.ar.resx     (Arabic)
/// </summary>
public sealed class ErrorMessages { }
```

**Why this path matters:** `IStringLocalizer<T>` resolves resource paths by stripping the
assembly's root namespace from the type's full name and prepending `ResourcesPath`. With this
namespace, .NET looks for `Resources/ErrorMessages.resx` — which is exactly where the `.resx`
files live. If you put the marker class under `HrSystemApp.Application.Localization` instead,
.NET would look for `Resources/Localization/ErrorMessages.resx` and silently fall back to English.

---

### Step 4 — Create the ErrorLocalizer implementation

**New file: `HrSystemApp.Application/Resources/ErrorLocalizer.cs`**

```csharp
using HrSystemApp.Application.Common;
using Microsoft.Extensions.Localization;

namespace HrSystemApp.Application.Resources;

public sealed class ErrorLocalizer : IErrorLocalizer
{
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public ErrorLocalizer(IStringLocalizer<ErrorMessages> localizer)
    {
        _localizer = localizer;
    }

    public Error Localize(Error error)
    {
        if (error == Error.None)
            return error;

        var localized = _localizer[error.Code];

        // ResourceNotFound = true means no entry exists in the resx for this key.
        // Fall back to the original English Message so nothing breaks.
        if (localized.ResourceNotFound)
            return error;

        return error with { Message = localized.Value };
    }
}
```

---

### Step 5 — Register IErrorLocalizer in DI

**Modify: `HrSystemApp.Application/DependencyInjection.cs`**

Add to `AddApplication()`:

```csharp
using HrSystemApp.Application.Resources;

// inside AddApplication():
services.AddScoped<IErrorLocalizer, ErrorLocalizer>();
```

Why `Scoped`? Because `IStringLocalizer<T>` is scoped (it reads `CultureInfo.CurrentUICulture`
which is set per-request by `UseRequestLocalization()`).

---

### Step 6 — Configure localization in Program.cs

**Modify: `HrSystemApp.Api/Program.cs`**

After `builder.Services.AddApplication()`, add:

```csharp
// Localization — must be called before UseRequestLocalization in the pipeline
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "ar" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
    // Reads Accept-Language header automatically — no custom code needed
});
```

In the middleware pipeline, add `UseRequestLocalization()` **before** all your custom middleware
so the culture is set before any request processing begins:

```csharp
// ── Middleware pipeline ───────────────────────────────────────────────────────

app.UseRequestLocalization();          // ← ADD THIS FIRST

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
// ... rest unchanged
```

---

### Step 7 — Localize in BaseApiController (PATH 1)

**Modify: `HrSystemApp.Api/Controllers/BaseApiController.cs`**

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Resources;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            var successResponse = new ApiResponse<T>(true, result.Value);
            return result.Value is null ? NoContent() : Ok(successResponse);
        }

        return HandleError(result.Error);
    }

    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
            return NoContent();

        return HandleError(result.Error);
    }

    private IActionResult HandleError(Error error)
    {
        // Resolve IErrorLocalizer from the request's service provider
        // rather than using property injection, to keep the base controller clean.
        var localizer = HttpContext.RequestServices.GetRequiredService<IErrorLocalizer>();
        var localizedError = localizer.Localize(error);
        var errorResponse = new ApiResponse<object>(false, null, localizedError);

        return localizedError.Code switch
        {
            "Auth.InvalidCredentials" or "Auth.InvalidOtp" => Unauthorized(errorResponse),
            "Auth.Unauthorized" => Unauthorized(errorResponse),
            "General.Forbidden" or "Auth.Forbidden" => Forbid(),
            var code when code.Contains("NotFound") => NotFound(errorResponse),
            _ => BadRequest(errorResponse)
        };
    }
}
```

---

### Step 8 — Localize in ExceptionMiddleware (PATH 2)

> ⚠️ The existing `HandleExceptionAsync` is `private static`. It must become a **non-static**
> instance method so it can access the injected `_localizer` field. Remove the `static`
> keyword when making these changes.

**Modify: `HrSystemApp.Api/Middleware/ExceptionMiddleware.cs`**

```csharp
using HrSystemApp.Application.Resources;
// ... existing usings

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly LoggingOptions _loggingOptions;
    private readonly IHostEnvironment _env;
    private readonly IErrorLocalizer _localizer;             // ← add

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env,
        IOptions<LoggingOptions> loggingOptions,
        IErrorLocalizer localizer)                          // ← add
    {
        _next = next;
        _logger = logger;
        _env = env;
        _loggingOptions = loggingOptions.Value;
        _localizer = localizer;                             // ← add
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var lastKnownState = new { Path = context.Request.Path.Value, Method = context.Request.Method };
            _logger.LogActionFailure(_loggingOptions, "UnhandledException", LogStage.Processing, ex, lastKnownState);
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)  // ← removed static
    {
        var (statusCode, error) = MapExceptionToError(exception);
        var localizedError = _localizer.Localize(error);   // ← localize before serializing

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ApiResponse<object>(false, null, localizedError);
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    // MapExceptionToError and GetValidationMessage stay exactly as they are
}
```

---

### Step 9 — Create the resource files and add to .csproj

The `.resx` files must be added to `HrSystemApp.Application.csproj` with
`<EmbeddedResource>` build action, otherwise they won't be compiled into the assembly
and the localizer will silently fall back to English at runtime.

**Add to `HrSystemApp.Application/HrSystemApp.Application.csproj`** inside the existing
`<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\ErrorMessages.resx" />
  <EmbeddedResource Include="Resources\ErrorMessages.ar.resx" />
</ItemGroup>
```

**`HrSystemApp.Application/Resources/ErrorMessages.resx`** (English — default fallback):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
    xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter</value></resheader>

  <!-- Auth -->
  <data name="Auth.InvalidCredentials" xml:space="preserve">
    <value>Invalid email or password.</value>
  </data>
  <data name="Auth.UserNotFound" xml:space="preserve">
    <value>User was not found.</value>
  </data>
  <data name="Auth.AccountInactive" xml:space="preserve">
    <value>Your account is inactive. Please contact HR.</value>
  </data>
  <data name="Auth.CompanyInactive" xml:space="preserve">
    <value>Your company's account is currently inactive. Please contact support.</value>
  </data>
  <data name="Auth.TokenNotFound" xml:space="preserve">
    <value>Authentication token was not found.</value>
  </data>
  <data name="Auth.TokenExpired" xml:space="preserve">
    <value>Authentication token has expired.</value>
  </data>
  <data name="Auth.Unauthorized" xml:space="preserve">
    <value>You are not authorized to perform this action.</value>
  </data>
  <data name="Auth.EmployeeBlockedStatus" xml:space="preserve">
    <value>Your account is not active. Please contact HR.</value>
  </data>
  <data name="Auth.InvalidRefreshToken" xml:space="preserve">
    <value>The refresh token is invalid.</value>
  </data>
  <data name="Auth.RefreshTokenExpired" xml:space="preserve">
    <value>The refresh token has expired.</value>
  </data>
  <data name="Auth.RefreshTokenRevoked" xml:space="preserve">
    <value>Suspicious activity detected: Refresh token reuse. All sessions revoked.</value>
  </data>
  <data name="Auth.ResetFailed" xml:space="preserve">
    <value>Failed to reset password.</value>
  </data>
  <data name="Auth.PasswordChangeFailed" xml:space="preserve">
    <value>Failed to change password.</value>
  </data>
  <data name="Auth.ForcedChangeNotRequired" xml:space="preserve">
    <value>User is not required to change their password via this endpoint.</value>
  </data>
  <data name="Auth.InvalidOtp" xml:space="preserve">
    <value>Invalid OTP code provided.</value>
  </data>
  <data name="Auth.Forbidden" xml:space="preserve">
    <value>You are not authorized to access this resource.</value>
  </data>

  <!-- User -->
  <data name="User.NotFound" xml:space="preserve">
    <value>User was not found.</value>
  </data>
  <data name="User.AlreadyExists" xml:space="preserve">
    <value>User with this email already exists.</value>
  </data>
  <data name="User.UpdateFailed" xml:space="preserve">
    <value>Failed to update user.</value>
  </data>
  <data name="User.DeleteFailed" xml:space="preserve">
    <value>Failed to delete user.</value>
  </data>
  <data name="User.InvalidOtp" xml:space="preserve">
    <value>Invalid OTP code provided.</value>
  </data>
  <data name="User.OtpMaxAttemptsReached" xml:space="preserve">
    <value>Maximum OTP attempts reached. Please request a new code.</value>
  </data>

  <!-- Employee -->
  <data name="Employee.NotFound" xml:space="preserve">
    <value>Employee was not found.</value>
  </data>
  <data name="Employee.AlreadyExists" xml:space="preserve">
    <value>An employee with this email or phone number already exists.</value>
  </data>
  <data name="Employee.CreationFailed" xml:space="preserve">
    <value>Failed to create employee account. Please try again.</value>
  </data>
  <data name="Employee.AlreadyInactive" xml:space="preserve">
    <value>Employee is already inactive.</value>
  </data>
  <data name="Employee.InvalidEmploymentStatus" xml:space="preserve">
    <value>Employment status value is invalid.</value>
  </data>

  <!-- Company -->
  <data name="Company.NotFound" xml:space="preserve">
    <value>Company was not found.</value>
  </data>

  <!-- Department -->
  <data name="Department.NotFound" xml:space="preserve">
    <value>Department was not found.</value>
  </data>
  <data name="Department.AlreadyExists" xml:space="preserve">
    <value>A department with this name already exists in the company.</value>
  </data>

  <!-- Unit -->
  <data name="Unit.NotFound" xml:space="preserve">
    <value>Unit was not found.</value>
  </data>
  <data name="Unit.AlreadyExists" xml:space="preserve">
    <value>A unit with this name already exists in the department.</value>
  </data>

  <!-- Team -->
  <data name="Team.NotFound" xml:space="preserve">
    <value>Team was not found.</value>
  </data>
  <data name="Team.AlreadyExists" xml:space="preserve">
    <value>A team with this name already exists in the unit.</value>
  </data>

  <!-- LeaveBalance -->
  <data name="LeaveBalance.NotFound" xml:space="preserve">
    <value>No leave balance found for this employee and year.</value>
  </data>
  <data name="LeaveBalance.AlreadyInitialized" xml:space="preserve">
    <value>Leave balance for this type and year already exists.</value>
  </data>
  <data name="LeaveBalance.Insufficient" xml:space="preserve">
    <value>Insufficient leave balance for the requested duration.</value>
  </data>
  <data name="LeaveBalance.InvalidDuration" xml:space="preserve">
    <value>Duration must be positive.</value>
  </data>

  <!-- Request -->
  <data name="Request.NotFound" xml:space="preserve">
    <value>Request was not found.</value>
  </data>
  <data name="Request.TypeDisabled" xml:space="preserve">
    <value>The requested type is not available for your company.</value>
  </data>
  <data name="Request.NotPending" xml:space="preserve">
    <value>Only pending requests can be modified or deleted.</value>
  </data>
  <data name="Request.ModificationLocked" xml:space="preserve">
    <value>Request is locked once approval process has started.</value>
  </data>
  <data name="Request.DefinitionNotFound" xml:space="preserve">
    <value>Request configuration for this type was not found.</value>
  </data>
  <data name="Request.DefinitionAlreadyExists" xml:space="preserve">
    <value>A request definition for this type already exists for this company.</value>
  </data>
  <data name="Request.Unauthorized" xml:space="preserve">
    <value>You are not the designated approver for this request at this stage.</value>
  </data>
  <data name="Request.Locked" xml:space="preserve">
    <value>Request is not in a state that can be approved or handled.</value>
  </data>
  <data name="Request.InvalidDuration" xml:space="preserve">
    <value>Duration must be positive.</value>
  </data>
  <data name="Request.NoActiveManagersAtStep" xml:space="preserve">
    <value>A workflow step has no active managers assigned.</value>
  </data>
  <data name="Request.InvalidWorkflowChain" xml:space="preserve">
    <value>Workflow step references a node not in the approval chain.</value>
  </data>
  <data name="Request.NotPendingApproval" xml:space="preserve">
    <value>This request is not currently awaiting approval.</value>
  </data>
  <data name="Request.StepOrderExceeded" xml:space="preserve">
    <value>Step order exceeds the number of workflow steps.</value>
  </data>
  <data name="Request.OrgNodeNotInCompany" xml:space="preserve">
    <value>The referenced org node does not belong to this company.</value>
  </data>
  <data name="Request.DirectEmployeeNotInCompany" xml:space="preserve">
    <value>The referenced employee does not belong to this company.</value>
  </data>
  <data name="Request.DirectEmployeeNotActive" xml:space="preserve">
    <value>The referenced employee is not active.</value>
  </data>
  <data name="Request.MissingDirectEmployeeId" xml:space="preserve">
    <value>A DirectEmployee step must specify a DirectEmployeeId.</value>
  </data>
  <data name="Request.MissingOrgNodeId" xml:space="preserve">
    <value>An OrgNode step must specify an OrgNodeId.</value>
  </data>
  <data name="Request.MissingCompanyRoleId" xml:space="preserve">
    <value>A CompanyRole step must specify a CompanyRoleId.</value>
  </data>
  <data name="Request.RoleNotInCompany" xml:space="preserve">
    <value>The referenced company role does not belong to this company.</value>
  </data>
  <data name="Request.RoleNotFound" xml:space="preserve">
    <value>The referenced company role was not found.</value>
  </data>
  <data name="Request.MissingLevelsUp" xml:space="preserve">
    <value>A HierarchyLevel step must specify LevelsUp greater than or equal to 1.</value>
  </data>
  <data name="Request.InvalidStartFromLevel" xml:space="preserve">
    <value>StartFromLevel must be greater than or equal to 1.</value>
  </data>
  <data name="Request.UnexpectedFieldsOnHierarchyLevelStep" xml:space="preserve">
    <value>HierarchyLevel steps must not have OrgNodeId, DirectEmployeeId, or BypassHierarchyCheck.</value>
  </data>
  <data name="Request.HierarchyLevelFieldsOnNonHierarchyStep" xml:space="preserve">
    <value>StartFromLevel and LevelsUp are only valid on HierarchyLevel steps.</value>
  </data>
  <data name="Request.HierarchyRangesOverlap" xml:space="preserve">
    <value>HierarchyLevel step ranges must not overlap.</value>
  </data>

  <!-- Notification -->
  <data name="Notification.NotFound" xml:space="preserve">
    <value>Notification was not found.</value>
  </data>
  <data name="Notification.Forbidden" xml:space="preserve">
    <value>You are not allowed to access this notification.</value>
  </data>
  <data name="Notification.SendFailed" xml:space="preserve">
    <value>Failed to send the notification.</value>
  </data>
  <data name="Notification.TokenMissing" xml:space="preserve">
    <value>The target employee does not have a valid FCM token.</value>
  </data>

  <!-- Attendance -->
  <data name="Attendance.NotFound" xml:space="preserve">
    <value>Attendance record was not found.</value>
  </data>
  <data name="Attendance.AlreadyClockedIn" xml:space="preserve">
    <value>Employee already has an open attendance record.</value>
  </data>
  <data name="Attendance.ClockInRequired" xml:space="preserve">
    <value>Employee must clock in before clocking out.</value>
  </data>
  <data name="Attendance.AlreadyClockedOut" xml:space="preserve">
    <value>Employee has already clocked out.</value>
  </data>
  <data name="Attendance.InvalidClockOut" xml:space="preserve">
    <value>Clock-out time must be after clock-in time.</value>
  </data>
  <data name="Attendance.OverrideReasonRequired" xml:space="preserve">
    <value>Override reason is required.</value>
  </data>

  <!-- Workflow -->
  <data name="Workflow.NotFound" xml:space="preserve">
    <value>No approval workflow defined for this request type.</value>
  </data>
  <data name="Workflow.InvalidStep" xml:space="preserve">
    <value>The workflow step is no longer valid or has changed.</value>
  </data>

  <!-- OrgNode -->
  <data name="OrgNode.NotFound" xml:space="preserve">
    <value>The requested organization node was not found.</value>
  </data>
  <data name="OrgNode.CircularReference" xml:space="preserve">
    <value>Cannot create a circular hierarchy reference.</value>
  </data>
  <data name="OrgNode.DuplicateAssignment" xml:space="preserve">
    <value>This employee is already assigned to this node.</value>
  </data>
  <data name="OrgNode.AssignmentNotFound" xml:space="preserve">
    <value>Assignment not found.</value>
  </data>
  <data name="OrgNode.InvalidHierarchyConfiguration" xml:space="preserve">
    <value>Invalid hierarchy configuration detected.</value>
  </data>

  <!-- Roles -->
  <data name="Roles.NotFound" xml:space="preserve">
    <value>The role was not found.</value>
  </data>
  <data name="Roles.NameAlreadyExists" xml:space="preserve">
    <value>A role with this name already exists in the company.</value>
  </data>
  <data name="Roles.InUseByWorkflow" xml:space="preserve">
    <value>This role is referenced by an active workflow definition and cannot be deleted.</value>
  </data>
  <data name="Roles.AlreadyAssigned" xml:space="preserve">
    <value>This role is already assigned to the employee.</value>
  </data>
  <data name="Roles.AssignmentNotFound" xml:space="preserve">
    <value>This role assignment does not exist.</value>
  </data>
  <data name="Roles.InvalidPermission" xml:space="preserve">
    <value>One or more permissions are not valid. Check AppPermissions for allowed values.</value>
  </data>

  <!-- Hierarchy -->
  <data name="Hierarchy.NotConfigured" xml:space="preserve">
    <value>The company hierarchy has not been configured yet.</value>
  </data>
  <data name="Hierarchy.InvalidRole" xml:space="preserve">
    <value>One or more roles are not valid for the company hierarchy. SuperAdmin is not allowed.</value>
  </data>
  <data name="Hierarchy.DuplicateRole" xml:space="preserve">
    <value>Each role can only appear once in the hierarchy configuration.</value>
  </data>
  <data name="Hierarchy.MultipleCeos" xml:space="preserve">
    <value>Only one CEO position can be configured per company.</value>
  </data>
  <data name="Hierarchy.WorkflowRoleNotInHierarchy" xml:space="preserve">
    <value>One or more workflow step roles are not configured in the company hierarchy.</value>
  </data>
  <data name="Hierarchy.InvalidStepOrder" xml:space="preserve">
    <value>Workflow steps must strictly escalate in authority order.</value>
  </data>
  <data name="Hierarchy.RoleInUse" xml:space="preserve">
    <value>Cannot remove role because it is currently being used in one or more active Request Definitions.</value>
  </data>

  <!-- Storage -->
  <data name="Storage.BucketNotFound" xml:space="preserve">
    <value>The storage bucket was not found.</value>
  </data>
  <data name="Storage.ObjectNotFound" xml:space="preserve">
    <value>The object was not found in storage.</value>
  </data>
  <data name="Storage.UploadFailed" xml:space="preserve">
    <value>Failed to upload the file to storage.</value>
  </data>
  <data name="Storage.DeleteFailed" xml:space="preserve">
    <value>Failed to delete the object from storage.</value>
  </data>
  <data name="Storage.ListFailed" xml:space="preserve">
    <value>Failed to list objects in storage.</value>
  </data>
  <data name="Storage.PresignedUrlFailed" xml:space="preserve">
    <value>Failed to generate a download URL.</value>
  </data>

  <!-- ContactAdmin -->
  <data name="ContactAdmin.NotFound" xml:space="preserve">
    <value>Contact admin request was not found.</value>
  </data>
  <data name="ContactAdmin.AlreadyProcessed" xml:space="preserve">
    <value>This request has already been processed.</value>
  </data>
  <data name="ContactAdmin.DuplicatePendingRequest" xml:space="preserve">
    <value>A pending request with this email or company name already exists.</value>
  </data>
  <data name="ContactAdmin.PhoneNumberAlreadyTaken" xml:space="preserve">
    <value>This phone number is already taken.</value>
  </data>
  <data name="ContactAdmin.EmailAlreadyTaken" xml:space="preserve">
    <value>This email is already taken.</value>
  </data>
  <data name="ContactAdmin.CompanyNameAlreadyTaken" xml:space="preserve">
    <value>This company name is already taken.</value>
  </data>

  <!-- ProfileUpdate -->
  <data name="ProfileUpdate.NotFound" xml:space="preserve">
    <value>Request not found.</value>
  </data>
  <data name="ProfileUpdate.NotPending" xml:space="preserve">
    <value>Only pending requests can be handled.</value>
  </data>
  <data name="ProfileUpdate.EmployeeNotFound" xml:space="preserve">
    <value>Employee not found. Data may be corrupted.</value>
  </data>
  <data name="ProfileUpdate.EmptyChanges" xml:space="preserve">
    <value>ChangesJson is empty for approved request. Cannot apply changes.</value>
  </data>
  <data name="ProfileUpdate.DeserializationFailed" xml:space="preserve">
    <value>Failed to deserialize ChangesJson for request.</value>
  </data>
  <data name="ProfileUpdate.UnknownField" xml:space="preserve">
    <value>Unknown field in ChangesJson.</value>
  </data>
  <data name="ProfileUpdate.HasPending" xml:space="preserve">
    <value>You already have a pending profile update request.</value>
  </data>
  <data name="ProfileUpdate.InvalidField" xml:space="preserve">
    <value>Field is not allowed for update.</value>
  </data>
  <data name="ProfileUpdate.NoChanges" xml:space="preserve">
    <value>No new or valid changes provided. The entered values map to your existing profile.</value>
  </data>
  <data name="ProfileUpdate.MalformedChanges" xml:space="preserve">
    <value>One or more fields in the changes payload are missing the required 'newValue' key.</value>
  </data>
  <data name="ProfileUpdate.InvalidLocationId" xml:space="preserve">
    <value>Invalid CompanyLocationId provided.</value>
  </data>

  <!-- Hr -->
  <data name="Hr.EmployeeNotFound" xml:space="preserve">
    <value>The acting user has no employee record.</value>
  </data>

  <!-- General -->
  <data name="General.ServerError" xml:space="preserve">
    <value>An unexpected error occurred.</value>
  </data>
  <data name="General.ValidationError" xml:space="preserve">
    <value>One or more validation errors occurred.</value>
  </data>
  <data name="General.NotFound" xml:space="preserve">
    <value>The requested resource was not found.</value>
  </data>
  <data name="General.ArgumentError" xml:space="preserve">
    <value>An invalid argument was provided.</value>
  </data>
  <data name="General.InvalidOperation" xml:space="preserve">
    <value>The requested operation is not valid in the current state.</value>
  </data>
  <data name="General.Forbidden" xml:space="preserve">
    <value>You are not authorized to access this resource.</value>
  </data>

  <!-- Validation -->
  <data name="Validation.FieldRequired" xml:space="preserve">
    <value>A required field is missing.</value>
  </data>
  <data name="Validation.InvalidType" xml:space="preserve">
    <value>Field has an invalid data type.</value>
  </data>
  <data name="Validation.Error" xml:space="preserve">
    <value>An error occurred during validation.</value>
  </data>
</root>
```

> For `ErrorMessages.ar.resx` — same XML structure, Arabic values. See Arabic translation table below.

---

## Phase 2 — Validation Messages (FluentValidation)

**Scope:** ~25 validator files with ~80+ individual rules. This is a large change and should be
completed separately from Phase 1.

### Step A — Add `.WithErrorCode()` to every validator rule

Every FluentValidation rule needs an error code alongside (or instead of) its English `.WithMessage()`.
The code becomes the localization key; the message becomes the English fallback.

```csharp
// Before:
RuleFor(x => x.Email)
    .NotEmpty().WithMessage(Messages.Validation.EmailRequired)
    .EmailAddress().WithMessage(Messages.Validation.ValidEmailRequired);

// After:
RuleFor(x => x.Email)
    .NotEmpty().WithErrorCode("Val.EmailRequired").WithMessage(Messages.Validation.EmailRequired)
    .EmailAddress().WithErrorCode("Val.ValidEmailRequired").WithMessage(Messages.Validation.ValidEmailRequired);
```

### Step B — Localize validation errors in ExceptionMiddleware

```csharp
// In ExceptionMiddleware — replace GetValidationMessage with this:
private string GetValidationMessage(ValidationException validationEx, IStringLocalizer<ValidationMessages> localizer)
{
    var messages = validationEx.Errors.Select(e =>
    {
        if (!string.IsNullOrEmpty(e.ErrorCode))
        {
            var localized = localizer[e.ErrorCode];
            if (!localized.ResourceNotFound)
                return localized.Value;
        }
        return e.ErrorMessage;
    });

    return string.Join("; ", messages);
}
```

Inject `IStringLocalizer<ValidationMessages>` into the middleware constructor alongside `IErrorLocalizer`.

### Step C — Create ValidationMessages.resx and ValidationMessages.ar.resx

Same `.resx` format as `ErrorMessages`. Keys are the `Val.*` codes added in Step A.
Arabic translations need to be populated (not included in this plan — translate all
`Messages.Validation.*` constants).

---

## Arabic Translations — ErrorMessages.ar.resx

| Key | Arabic Translation |
|-----|-------------------|
| Auth.InvalidCredentials | البريد الإلكتروني أو كلمة المرور غير صحيحة. |
| Auth.UserNotFound | لم يتم العثور على المستخدم. |
| Auth.AccountInactive | حسابك غير نشط. يرجى التواصل مع الموارد البشرية. |
| Auth.CompanyInactive | حساب شركتك غير نشط حالياً. يرجى التواصل مع الدعم. |
| Auth.TokenNotFound | لم يتم العثور على رمز المصادقة. |
| Auth.TokenExpired | انتهت صلاحية رمز المصادقة. |
| Auth.Unauthorized | غير مصرح لك بتنفيذ هذا الإجراء. |
| Auth.EmployeeBlockedStatus | حسابك غير نشط. يرجى التواصل مع الموارد البشرية. |
| Auth.InvalidRefreshToken | رمز التحديث غير صالح. |
| Auth.RefreshTokenExpired | انتهت صلاحية رمز التحديث. |
| Auth.RefreshTokenRevoked | تم إلغاء رمز التحديث. |
| Auth.RefreshTokenReused | تم رصد نشاط مشبوه: إعادة استخدام رمز التحديث. تم إلغاء جميع الجلسات. |
| Auth.ResetFailed | فشل إعادة تعيين كلمة المرور. |
| Auth.PasswordChangeFailed | فشل تغيير كلمة المرور. |
| Auth.ForcedChangeNotRequired | المستخدم غير مطالب بتغيير كلمة المرور عبر هذه النقطة. |
| Auth.InvalidOtp | رمز التحقق المُدخل غير صالح. |
| Auth.Forbidden | غير مصرح لك بالوصول إلى هذا المورد. |
| User.NotFound | لم يتم العثور على المستخدم. |
| User.AlreadyExists | مستخدم بهذا البريد الإلكتروني موجود مسبقاً. |
| User.UpdateFailed | فشل تحديث المستخدم. |
| User.DeleteFailed | فشل حذف المستخدم. |
| User.InvalidOtp | رمز التحقق المُدخل غير صالح. |
| User.OtpMaxAttemptsReached | تم الوصول إلى الحد الأقصى لمحاولات التحقق. يرجى طلب رمز جديد. |
| Employee.NotFound | لم يتم العثور على الموظف. |
| Employee.AlreadyExists | موظف بهذا البريد أو رقم الهاتف موجود مسبقاً. |
| Employee.CreationFailed | فشل إنشاء حساب الموظف. يرجى المحاولة مجدداً. |
| Employee.AlreadyInactive | الموظف غير نشط مسبقاً. |
| Employee.InvalidEmploymentStatus | قيمة حالة التوظيف غير صالحة. |
| Company.NotFound | لم يتم العثور على الشركة. |
| Department.NotFound | لم يتم العثور على القسم. |
| Department.AlreadyExists | قسم بهذا الاسم موجود مسبقاً في الشركة. |
| Unit.NotFound | لم يتم العثور على الوحدة. |
| Unit.AlreadyExists | وحدة بهذا الاسم موجودة مسبقاً في القسم. |
| Team.NotFound | لم يتم العثور على الفريق. |
| Team.AlreadyExists | فريق بهذا الاسم موجود مسبقاً في الوحدة. |
| LeaveBalance.NotFound | لم يتم العثور على سجل رصيد الإجازة لهذا الموظف والسنة. |
| LeaveBalance.AlreadyInitialized | رصيد الإجازة لهذا النوع والسنة موجود مسبقاً. |
| LeaveBalance.Insufficient | رصيد الإجازة غير كافٍ للمدة المطلوبة. |
| LeaveBalance.InvalidDuration | يجب أن تكون المدة أكبر من صفر. |
| Request.NotFound | لم يتم العثور على الطلب. |
| Request.TypeDisabled | نوع الطلب المحدد غير متاح لشركتك. |
| Request.NotPending | يمكن تعديل أو حذف الطلبات المعلقة فقط. |
| Request.ModificationLocked | الطلب مقفل بعد بدء عملية الموافقة. |
| Request.DefinitionNotFound | لم يتم العثور على إعداد الطلب لهذا النوع. |
| Request.DefinitionAlreadyExists | تعريف طلب لهذا النوع موجود مسبقاً لهذه الشركة. |
| Request.Unauthorized | لست المعتمد المخصص لهذا الطلب في هذه المرحلة. |
| Request.Locked | الطلب ليس في حالة تسمح بالموافقة أو المعالجة. |
| Request.InvalidDuration | يجب أن تكون المدة أكبر من صفر. |
| Request.NoActiveManagersAtStep | لا يوجد مديرون نشطون معينون لخطوة سير العمل هذه. |
| Request.InvalidWorkflowChain | خطوة سير العمل تشير إلى عقدة غير موجودة في سلسلة الموافقة. |
| Request.NotPendingApproval | هذا الطلب لا ينتظر الموافقة حالياً. |
| Request.StepOrderExceeded | ترتيب الخطوة يتجاوز عدد خطوات سير العمل. |
| Request.OrgNodeNotInCompany | عقدة التنظيم المُشار إليها لا تنتمي إلى هذه الشركة. |
| Request.DirectEmployeeNotInCompany | الموظف المُحيل لا ينتمي إلى هذه الشركة. |
| Request.DirectEmployeeNotActive | الموظف المُحيل غير نشط. |
| Request.MissingDirectEmployeeId | خطوة الموظف المباشر يجب أن تحدد DirectEmployeeId. |
| Request.MissingOrgNodeId | خطوة عقدة التنظيم يجب أن تحدد OrgNodeId. |
| Request.MissingCompanyRoleId | خطوة دور الشركة يجب أن تحدد CompanyRoleId. |
| Request.RoleNotInCompany | الدور المُشار إليه لا ينتمي إلى هذه الشركة. |
| Request.RoleNotFound | لم يتم العثور على دور الشركة المُشار إليه. |
| Request.MissingLevelsUp | خطوة مستوى الهيكل يجب أن تحدد LevelsUp أكبر من أو يساوي 1. |
| Request.InvalidStartFromLevel | StartFromLevel يجب أن يكون أكبر من أو يساوي 1. |
| Request.UnexpectedFieldsOnHierarchyLevelStep | خطوات مستوى الهيكل يجب ألا تحتوي على OrgNodeId أو DirectEmployeeId أو BypassHierarchyCheck. |
| Request.HierarchyLevelFieldsOnNonHierarchyStep | StartFromLevel و LevelsUp صالحان فقط في خطوات مستوى الهيكل. |
| Request.HierarchyRangesOverlap | نطاقات خطوات مستوى الهيكل يجب ألا تتداخل. |
| Notification.NotFound | لم يتم العثور على الإشعار. |
| Notification.Forbidden | غير مسموح لك بالوصول إلى هذا الإشعار. |
| Notification.SendFailed | فشل إرسال الإشعار. |
| Notification.TokenMissing | الموظف المستهدف لا يمتلك رمز FCM صالح. |
| Attendance.NotFound | لم يتم العثور على سجل الحضور. |
| Attendance.AlreadyClockedIn | الموظف لديه سجل حضور مفتوح مسبقاً. |
| Attendance.ClockInRequired | يجب على الموظف تسجيل الحضور قبل تسجيل الانصراف. |
| Attendance.AlreadyClockedOut | سجّل الموظف انصرافه مسبقاً. |
| Attendance.InvalidClockOut | وقت الانصراف يجب أن يكون بعد وقت الحضور. |
| Attendance.OverrideReasonRequired | سبب التجاوز مطلوب. |
| Workflow.NotFound | لا يوجد سير موافقة محدد لهذا النوع من الطلبات. |
| Workflow.InvalidStep | خطوة سير العمل لم تعد صالحة أو تغيرت. |
| OrgNode.NotFound | لم يتم العثور على العقدة التنظيمية المطلوبة. |
| OrgNode.CircularReference | لا يمكن إنشاء مرجع هرمي دائري. |
| OrgNode.DuplicateAssignment | هذا الموظف معين مسبقاً لهذه العقدة. |
| OrgNode.AssignmentNotFound | لم يتم العثور على التعيين. |
| OrgNode.InvalidHierarchyConfiguration | تم اكتشاف تكوين هيكل تنظيمي غير صالح. |
| Roles.NotFound | لم يتم العثور على الدور. |
| Roles.NameAlreadyExists | دور بهذا الاسم موجود مسبقاً في الشركة. |
| Roles.InUseByWorkflow | لا يمكن حذف هذا الدور لأنه مستخدم في تعريف سير عمل نشط. |
| Roles.AlreadyAssigned | هذا الدور معين مسبقاً للموظف. |
| Roles.AssignmentNotFound | هذا التعيين غير موجود. |
| Roles.InvalidPermission | واحد أو أكثر من الأذونات غير صالحة. راجع AppPermissions للقيم المسموح بها. |
| Hierarchy.NotConfigured | لم يتم إعداد هيكل الشركة التنظيمي بعد. |
| Hierarchy.InvalidRole | أحد الأدوار أو أكثر غير صالح في الهيكل التنظيمي للشركة. |
| Hierarchy.DuplicateRole | يمكن لكل دور أن يظهر مرة واحدة فقط في إعداد الهيكل التنظيمي. |
| Hierarchy.MultipleCeos | يمكن تكوين منصب رئيس تنفيذي واحد فقط لكل شركة. |
| Hierarchy.WorkflowRoleNotInHierarchy | أحد أدوار خطوات سير العمل أو أكثر غير مُعد في هيكل الشركة التنظيمي. |
| Hierarchy.InvalidStepOrder | خطوات سير العمل يجب أن ترتقي بصرامة في ترتيب السلطة. |
| Hierarchy.RoleInUse | لا يمكن إزالة الدور لأنه مستخدم حالياً في تعريفات طلبات نشطة. |
| Storage.BucketNotFound | لم يتم العثور على حاوية التخزين. |
| Storage.ObjectNotFound | لم يتم العثور على الملف في التخزين. |
| Storage.UploadFailed | فشل رفع الملف إلى التخزين. |
| Storage.DeleteFailed | فشل حذف الملف من التخزين. |
| Storage.ListFailed | فشل سرد الملفات في التخزين. |
| Storage.PresignedUrlFailed | فشل توليد رابط التحميل. |
| ContactAdmin.NotFound | لم يتم العثور على طلب التواصل مع المسؤول. |
| ContactAdmin.AlreadyProcessed | تمت معالجة هذا الطلب مسبقاً. |
| ContactAdmin.DuplicatePendingRequest | يوجد طلب معلق بهذا البريد أو اسم الشركة مسبقاً. |
| ContactAdmin.PhoneNumberAlreadyTaken | رقم الهاتف هذا مستخدم مسبقاً. |
| ContactAdmin.EmailAlreadyTaken | البريد الإلكتروني هذا مستخدم مسبقاً. |
| ContactAdmin.CompanyNameAlreadyTaken | اسم الشركة هذا مستخدم مسبقاً. |
| ProfileUpdate.NotFound | لم يتم العثور على الطلب. |
| ProfileUpdate.NotPending | يمكن معالجة الطلبات المعلقة فقط. |
| ProfileUpdate.EmployeeNotFound | الموظف غير موجود. قد تكون البيانات تالفة. |
| ProfileUpdate.EmptyChanges | ChangesJson فارغ للطلب المعتمد. لا يمكن تطبيق التغييرات. |
| ProfileUpdate.DeserializationFailed | فشل إلغاء تسلسل ChangesJson للطلب. |
| ProfileUpdate.UnknownField | حقل غير معروف في ChangesJson. |
| ProfileUpdate.HasPending | لديك بالفعل طلب تحديث ملف شخصي معلق. |
| ProfileUpdate.InvalidField | الحقل غير مسموح بتحديثه. |
| ProfileUpdate.NoChanges | لم يتم تقديم تغييرات جديدة أو صالحة. القيم المدخلة مطابقة لملفك الحالي. |
| ProfileUpdate.MalformedChanges | واحد أو أكثر من الحقول في حمولة التغييرات يفتقد مفتاح 'newValue' المطلوب. |
| ProfileUpdate.InvalidLocationId | CompanyLocationId غير صالح. |
| Hr.EmployeeNotFound | المستخدم الحالي لا يمتلك سجل موظف. |
| General.ServerError | حدث خطأ غير متوقع. |
| General.ValidationError | حدث خطأ في التحقق من صحة البيانات. |
| General.NotFound | المورد المطلوب غير موجود. |
| General.ArgumentError | تم تقديم معامل غير صالح. |
| General.InvalidOperation | العملية المطلوبة غير صالحة في الحالة الحالية. |
| General.Forbidden | غير مصرح لك بالوصول إلى هذا المورد. |
| Validation.FieldRequired | حقل مطلوب مفقود. |
| Validation.InvalidType | نوع البيانات غير صالح. |
| Validation.Error | حدث خطأ أثناء التحقق. |

---

## How the client uses it

The client (mobile app or web) sends one header:

```
Accept-Language: ar
```

That's it. Every error response in the system automatically comes back in Arabic.
No query strings, no extra endpoints, no database lookups.

If the client sends nothing, or sends `Accept-Language: en`, all responses are in English.

---

## Verifying localization works

After implementing Phase 1, test with:

```bash
curl -H "Accept-Language: ar" https://localhost:5001/api/auth/login \
  -d '{"email":"x","password":"y"}' -H "Content-Type: application/json"
```

Expected: `"message": "البريد الإلكتروني أو كلمة المرور غير صحيحة."`

If you see the English message instead, check:
1. `UseRequestLocalization()` is before all other middleware in the pipeline
2. `ErrorMessages.ar.resx` is included in the `.csproj` with `<EmbeddedResource>`
3. `IErrorLocalizer` is registered as `AddScoped`

---

## Summary — what changes and what does not

| | Changed? |
|---|---|
| `DomainErrors.cs` | ❌ No change |
| `Messages.cs` | ❌ No change |
| All handlers (`Result.Failure(...)`) | ❌ No change |
| `Error.cs` record | ❌ No change |
| `ApiResponse.cs` | ❌ No change |
| `BaseApiController.HandleError()` | ✅ Resolve `IErrorLocalizer` from `HttpContext.RequestServices` + one localize call |
| `ExceptionMiddleware` | ✅ Inject `IErrorLocalizer`, remove `static`, call `_localizer.Localize()` |
| `Program.cs` | ✅ `AddLocalization()` + `UseRequestLocalization()` before all middleware |
| `Application.csproj` | ✅ `Microsoft.Extensions.Localization.Abstractions` NuGet + `<EmbeddedResource>` for resx files |
| New: `IErrorLocalizer`, `ErrorLocalizer` | ✅ New files in `HrSystemApp.Application/Resources/` |
| New: `ErrorMessages.cs`, `ErrorMessages.resx`, `ErrorMessages.ar.resx` | ✅ New files |
