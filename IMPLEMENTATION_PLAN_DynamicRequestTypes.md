# Option C: Dynamic Request Types - Implementation Plan

## Overview
Change `RequestType` from enum (int) to string, allowing users to create custom request types with their own schemas.

---

## Phase 1: Database Migration

### 1.1 New Table: `RequestTypeDefinitions`

```sql
CREATE TABLE RequestTypeDefinitions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TypeName NVARCHAR(100) NOT NULL,        -- e.g., "Leave", "EquipmentRequest", "Complaint"
    CompanyId UNIQUEIDENTIFIER NULL,       -- NULL = system type, else company-specific
    DisplayName NVARCHAR(200) NOT NULL,     -- e.g., "Leave Request", "Equipment Request"
    Description NVARCHAR(500) NULL,
    FormSchemaJson NVARCHAR(MAX) NULL,      -- Custom schema (overrides global if set)
    IsSystemType BIT NOT NULL DEFAULT 0,    -- 1 = predefined type (Leave, Survey, etc.)
    IsActive BIT NOT NULL DEFAULT 1,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NULL,

    CONSTRAINT FK_RequestTypeDefinitions_Company FOREIGN KEY (CompanyId) REFERENCES Companies(Id)
);

-- Unique constraint: type name per company
CREATE UNIQUE INDEX UX_RequestTypeDefinition_TypeName_Company
    ON RequestTypeDefinitions(TypeName, CompanyId) WHERE CompanyId IS NOT NULL;
```

### 1.2 Alter Existing Tables

```sql
-- Request table
ALTER TABLE Requests ALTER COLUMN RequestType NVARCHAR(100) NOT NULL;

-- RequestDefinition table
ALTER TABLE RequestDefinitions ALTER COLUMN RequestType NVARCHAR(100) NOT NULL;

-- RequestDefinitionId is NOT changed - still references RequestDefinition (workflow), not RequestType
```

### 1.3 Seed Data

```sql
-- System types (CompanyId = NULL, IsSystemType = 1)
INSERT INTO RequestTypeDefinitions (Id, TypeName, DisplayName, IsSystemType, SortOrder, CreatedAt)
VALUES
    (NEWID(), 'Leave', 'Leave Request', 1, 1, GETUTCDATE()),
    (NEWID(), 'Permission', 'Permission Request', 1, 2, GETUTCDATE()),
    (NEWID(), 'SalarySlip', 'Salary Slip Request', 1, 3, GETUTCDATE()),
    (NEWID(), 'HRLetter', 'HR Letter Request', 1, 4, GETUTCDATE()),
    (NEWID(), 'Resignation', 'Resignation Request', 1, 5, GETUTCDATE()),
    (NEWID(), 'EndOfService', 'End of Service Request', 1, 6, GETUTCDATE()),
    (NEWID(), 'PurchaseOrder', 'Purchase Order Request', 1, 7, GETUTCDATE()),
    (NEWID(), 'Asset', 'Asset Request', 1, 8, GETUTCDATE()),
    (NEWID(), 'Loan', 'Loan Request', 1, 9, GETUTCDATE()),
    (NEWID(), 'Assignment', 'Assignment Request', 1, 10, GETUTCDATE()),
    (NEWID(), 'Other', 'Other Request', 1, 11, GETUTCDATE()),
    (NEWID(), 'Survey', 'Survey Request', 1, 12, GETUTCDATE()),
    (NEWID(), 'Complaint', 'Complaint Request', 1, 13, GETUTCDATE());
```

---

## Phase 2: Domain Layer Changes

### 2.1 New File: `RequestTypeDefinition.cs`

**Location:** `HrSystemApp.Domain/Models/RequestTypeDefinition.cs`

```csharp
namespace HrSystemApp.Domain.Models;

public class RequestTypeDefinition : AuditableEntity
{
    public string TypeName { get; set; } = string.Empty;  // Primary key (string)
    public Guid? CompanyId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FormSchemaJson { get; set; }
    public bool IsSystemType { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public Company? Company { get; set; }
}
```

### 2.2 Modify: `Request.cs`

**Before:**
```csharp
public class Request : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public RequestType RequestType { get; set; }  // enum
    public string Data { get; set; } = "{}";
    // ...
}
```

**After:**
```csharp
public class Request : AuditableEntity
{
    public Guid EmployeeId { get; set; }
    public string RequestType { get; set; } = string.Empty;  // string (e.g., "Leave", "EquipmentRequest")
    public string Data { get; set; } = "{}";
    // ...
}
```

### 2.3 Modify: `RequestWorkflow.cs`

**Before:**
```csharp
public class RequestDefinition : AuditableEntity, IHardDelete
{
    public Guid CompanyId { get; set; }
    public RequestType RequestType { get; set; }  // enum
    // ...
}
```

**After:**
```csharp
public class RequestDefinition : AuditableEntity, IHardDelete
{
    public Guid CompanyId { get; set; }
    public string RequestType { get; set; } = string.Empty;  // string
    // ...
}
```

### 2.4 Keep: `RequestType.cs` as enum (Backward Compatibility)

**Option:** Keep enum for existing code that strongly types it, but add extension methods:

```csharp
namespace HrSystemApp.Domain.Enums;

public enum RequestType
{
    Leave = 0,
    Permission = 1,
    // ... existing values
}

public static class RequestTypeExtensions
{
    public static string ToTypeName(this RequestType rt) => rt.ToString();
    public static RequestType? FromTypeName(string name) =>
        Enum.TryParse<RequestType>(name, out var result) ? result : null;
}
```

**OR remove enum entirely** (more breaking but cleaner long-term).

---

## Phase 3: Application Layer Changes

### 3.1 Modify: `IRequestSchemaValidator.cs`

**Before:**
```csharp
public interface IRequestSchemaValidator
{
    Result Validate(RequestType type, string jsonData, string? customSchema = null);
    object GetSchema(RequestType type, string? customSchema = null);
}
```

**After:**
```csharp
public interface IRequestSchemaValidator
{
    Result Validate(string typeName, string jsonData, string? customSchema = null);
    object GetSchema(string typeName, string? customSchema = null);
}
```

### 3.2 Modify: `RequestSchemaValidator.cs`

```csharp
public Result Validate(string typeName, string jsonData, string? customSchema = null)
{
    // Look up schema: customSchema first, then global RequestSchemas.json
    var typeName = type;  // use string directly
    var schemaElement = !string.IsNullOrEmpty(customSchema)
        ? JsonDocument.Parse(customSchema).RootElement
        : _globalSchemas.RootElement.GetProperty("Schemas").GetProperty(typeName);  // no .ToString() needed

    // ... rest of validation
}

public object GetSchema(string typeName, string? customSchema = null)
{
    if (!string.IsNullOrEmpty(customSchema))
        return JsonSerializer.Deserialize<object>(customSchema)!;

    if (_globalSchemas.RootElement.GetProperty("Schemas").TryGetProperty(typeName, out var schema))
        return JsonSerializer.Deserialize<object>(schema.GetRawText())!;

    return new List<object>();
}
```

### 3.3 Modify: `IRequestBusinessStrategy.cs`

**Before:**
```csharp
public interface IRequestBusinessStrategy
{
    RequestType Type { get; }
    Task<Result> ValidateBusinessRulesAsync(Guid employeeId, JsonElement data, CancellationToken ct);
    Task OnFinalApprovalAsync(Request request, CancellationToken ct);
}
```

**After:**
```csharp
public interface IRequestBusinessStrategy
{
    string Type { get; }  // Changed from RequestType to string
    Task<Result> ValidateBusinessRulesAsync(Guid employeeId, JsonElement data, CancellationToken ct);
    Task OnFinalApprovalAsync(Request request, CancellationToken ct);
}
```

### 3.4 Modify: `RequestStrategyFactory.cs`

**Before:**
```csharp
public class RequestStrategyFactory : IRequestStrategyFactory
{
    public IRequestBusinessStrategy? GetStrategy(RequestType type)
    {
        return _strategies.FirstOrDefault(s => s.Type == type);
    }
}
```

**After:**
```csharp
public class RequestStrategyFactory : IRequestStrategyFactory
{
    public IRequestBusinessStrategy? GetStrategy(string typeName)
    {
        return _strategies.FirstOrDefault(s => s.Type == typeName);
    }
}
```

### 3.5 Modify: `LeaveRequestStrategy.cs`

**Before:**
```csharp
public class LeaveRequestStrategy : IRequestBusinessStrategy
{
    public RequestType Type => RequestType.Leave;
    // ...
}
```

**After:**
```csharp
public class LeaveRequestStrategy : IRequestBusinessStrategy
{
    public string Type => "Leave";  // Changed from RequestType to string
    // ...
}
```

### 3.6 Modify: `CreateRequestCommand.cs`

**Before:**
```csharp
public record CreateRequestCommand(RequestType RequestType, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;
```

**After:**
```csharp
public record CreateRequestCommand(string RequestType, JsonElement Data, string? Details = null) : IRequest<Result<Guid>>;
```

**Handler changes in `CreateRequestCommandHandler.cs`:**

```csharp
// Line 68 - Query by string instead of enum
var definition = await _unitOfWork.RequestDefinitions.GetByTypeAsync(
    employee.CompanyId, request.RequestType, cancellationToken);  // request.RequestType is now string

// Line 76 - Pass string
var schemaResult = _schemaValidator.Validate(request.RequestType, jsonData, definition.FormSchemaJson);

// Line 87 - Get strategy with string
var strategy = _strategyFactory.GetStrategy(request.RequestType);

// Line 198 - No change needed, uses string
newRequest.RequestType = request.RequestType;
```

### 3.7 Modify: `CreateRequestDefinitionCommand.cs`

**Before:**
```csharp
public record CreateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid? CompanyId { get; set; }
    public RequestType RequestType { get; set; }  // enum
    public List<WorkflowStepDto> Steps { get; set; } = new();
}
```

**After:**
```csharp
public record CreateRequestDefinitionCommand : IRequest<Result<Guid>>
{
    public Guid? CompanyId { get; set; }
    public string RequestType { get; set; } = string.Empty;  // string
    public List<WorkflowStepDto> Steps { get; set; } = new();
}
```

### 3.8 Modify: All Query DTOs using RequestType

Files to update:
- `GetUserRequestsQuery.cs`
- `GetCompanyRequestsQuery.cs`
- `GetRequestByIdQuery.cs`
- `GetMyApprovalActionsQuery.cs`
- `GetPendingApprovalsQuery.cs`
- `GetRequestDefinitionsQuery.cs`

**Before:**
```csharp
public class GetUserRequestsQuery : IRequest<Result<List<RequestDto>>>
{
    public Guid EmployeeId { get; set; }
    public RequestType? Type { get; set; }  // nullable enum filter
    // ...
}
```

**After:**
```csharp
public class GetUserRequestsQuery : IRequest<Result<List<RequestDto>>>
{
    public Guid EmployeeId { get; set; }
    public string? Type { get; set; }  // nullable string filter
    // ...
}
```

### 3.9 New: `RequestTypeDefinitionRepository`

```csharp
public interface IRequestTypeDefinitionRepository
{
    Task<RequestTypeDefinition?> GetByTypeNameAsync(string typeName, Guid? companyId = null, CancellationToken ct = default);
    Task<List<RequestTypeDefinition>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<List<RequestTypeDefinition>> GetSystemTypesAsync(CancellationToken ct = default);
    Task AddAsync(RequestTypeDefinition definition, CancellationToken ct = default);
    Task UpdateAsync(RequestTypeDefinition definition, CancellationToken ct = default);
}

public class RequestTypeDefinitionRepository : IRequestTypeDefinitionRepository
{
    public async Task<RequestTypeDefinition?> GetByTypeNameAsync(string typeName, Guid? companyId = null, CancellationToken ct = default)
    {
        // If companyId provided, try company-specific first, then fallback to system type
        // Query: WHERE TypeName == typeName AND (CompanyId == companyId OR (CompanyId IS NULL AND IsSystemType == 1))
    }

    public async Task<List<RequestTypeDefinition>> GetAllForCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        // Returns system types + company-specific types
    }

    public async Task<List<RequestTypeDefinition>> GetSystemTypesAsync(CancellationToken ct = default)
    {
        // Returns only system types (IsSystemType = 1 AND CompanyId IS NULL)
    }
}
```

---

## Phase 4: Infrastructure Layer Changes

### 4.1 Modify: `RequestDefinitionRepository.cs`

```csharp
public class RequestDefinitionRepository : IRequestDefinitionRepository
{
    // In GetByTypeAsync:
    public async Task<RequestDefinition?> GetByTypeAsync(Guid companyId, string requestType, CancellationToken ct = default)
    {
        return await _dbContext.RequestDefinitions
            .Include(rd => rd.WorkflowSteps)
            .FirstOrDefaultAsync(rd =>
                rd.CompanyId == companyId &&
                rd.RequestType == requestType &&  // string comparison, not enum
                !rd.IsDeleted, ct);
    }
}
```

### 4.2 Modify: `WorkflowService.cs`

```csharp
// Any place comparing RequestType enum to enum values
// Must change to string comparisons

// Before:
if (step.StepType == WorkflowStepType.OrgNode)

// After: (no change needed, WorkflowStepType is still enum)

// But for RequestType comparisons:
// Before:
if (request.RequestType == RequestType.Leave)

// After:
if (request.RequestType == "Leave")
```

### 4.3 Modify: `IRequestDefinitionRepository.cs`

```csharp
public interface IRequestDefinitionRepository
{
    Task<RequestDefinition?> GetByTypeAsync(Guid companyId, string requestType, CancellationToken ct = default);
    // ... other methods
}
```

---

## Phase 5: API Layer Changes

### 5.1 Modify: `RequestDefinitionsController.cs`

```csharp
// GET /api/request-definitions/types
// Returns available request types for the company
[HttpGet("types")]
public async Task<IActionResult> GetTypes()
{
    // Returns list of RequestTypeDefinitionDto with TypeName (string), DisplayName, etc.
    // For SuperAdmin: returns all system types
    // For CompanyAdmin: returns system types + company's custom types
}

// POST /api/request-definitions
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateRequestDefinitionRequest request)
{
    // request.RequestType is now string, e.g., "Leave" or "EquipmentRequest"
}

// PUT /api/request-definitions/{typeName}
[HttpPut("{typeName}")]
public async Task<IActionResult> Update(string typeName, [FromBody] UpdateRequestDefinitionRequest request)
{
    // typeName as route parameter (string, not int)
}
```

### 5.2 Modify: `RequestsController.cs`

```csharp
// GET /api/requests?type=Leave
// type parameter is now string

// POST /api/requests
public async Task<IActionResult> Create([FromBody] CreateRequestDto request)
{
    // request.RequestType is string, e.g., "Leave", "EquipmentRequest"
}
```

### 5.3 New Endpoint: Custom Type Management

```csharp
[HttpPost("custom-types")]
[Authorize(Roles = "ExecutiveOrAbove")]  // Only high-level roles can create custom types
public async Task<IActionResult> CreateCustomType([FromBody] CreateCustomRequestTypeRequest request)
{
    // request.TypeName = "EquipmentRequest"
    // request.DisplayName = "Equipment Request"
    // request.FormSchemaJson = {...}
}

[HttpGet("custom-types")]
public async Task<IActionResult> GetCustomTypes()
{
    // Returns company's custom request types
}

[HttpDelete("custom-types/{typeName}")]
public async Task<IActionResult> DeleteCustomType(string typeName)
{
    // Only deletes if !IsSystemType
}
```

---

## Phase 6: Frontend/Mobile Impact

### 6.1 API Response Changes

**Before (int enum):**
```json
{
  "requestType": 0,
  "requestTypeName": "Leave"
}
```

**After (string):**
```json
{
  "requestType": "Leave",
  "requestTypeDisplayName": "Leave Request"
}
```

### 6.2 Client-Side Changes

| Mobile/Frontend File | Change |
|---------------------|--------|
| `RequestType` model | Change from `enum { 0: Leave, 1: Permission... }` to `string` or constant map |
| All API calls using request type | Pass `"Leave"` instead of `0` |
| Schema fetching | `/api/request-definitions/schemas?typeName=Leave` |

---

## Phase 7: Testing Updates

### 7.1 Update Test Files

| Test File | Changes |
|-----------|---------|
| `CreateRequestDefinitionValidationTests.cs` | Use `"Leave"` string instead of `RequestType.Leave` |
| Any strategy tests | `LeaveRequestStrategy.Type` returns `"Leave"` not `.Leave` |

### 7.2 Example Test Change

**Before:**
```csharp
[Fact]
public async Task CreateRequest_WithLeaveType_ShouldSucceed()
{
    var command = new CreateRequestCommand(RequestType.Leave, data, null);
    // ...
}
```

**After:**
```csharp
[Fact]
public async Task CreateRequest_WithLeaveType_ShouldSucceed()
{
    var command = new CreateRequestCommand("Leave", data, null);
    // ...
}
```

---

## Summary: Files to Change

### New Files (2)
1. `HrSystemApp.Domain/Models/RequestTypeDefinition.cs`
2. `HrSystemApp.Application/Interfaces/Repositories/IRequestTypeDefinitionRepository.cs` (and implementation)

### Modify (16 files)

| Layer | Files |
|-------|-------|
| **Domain** | `Request.cs`, `RequestWorkflow.cs` |
| **Application** | `IRequestSchemaValidator.cs`, `RequestSchemaValidator.cs`, `IRequestBusinessStrategy.cs`, `RequestStrategyFactory.cs`, `LeaveRequestStrategy.cs`, `CreateRequestCommand.cs`, `CreateRequestDefinitionCommand.cs`, `GetUserRequestsQuery.cs`, `GetCompanyRequestsQuery.cs`, `GetRequestByIdQuery.cs`, `GetMyApprovalActionsQuery.cs`, `GetPendingApprovalsQuery.cs` |
| **Infrastructure** | `RequestDefinitionRepository.cs`, `IRequestDefinitionRepository.cs`, `WorkflowService.cs` |
| **API** | `RequestDefinitionsController.cs`, `RequestsController.cs` |

### Migrations (1-2)
1. Create `RequestTypeDefinitions` table
2. Alter `Requests.RequestType` and `RequestDefinitions.RequestType` to NVARCHAR(100)
3. Seed system types

---

## Breaking Changes Summary

| What Breaks | How to Fix |
|-------------|------------|
| All code comparing `RequestType` enum directly | Change to string comparison |
| Database queries using enum int values | Change to string comparison |
| Mobile client sending int for request type | Change to send string |
| Any hardcoded enum switches (`case RequestType.Leave:`) | Change to `case "Leave":` |
| Strategy pattern matching by enum | Change to string matching |

---

## Migration Sequence

1. **Create migration:** Add `RequestTypeDefinitions` table + alter columns
2. **Seed data:** Insert system types
3. **Deploy domain changes:** `RequestType` string property
4. **Deploy application layer:** String-based validators and factories
5. **Deploy API:** String type names in routes/requests
6. **Update mobile client:** Send/receive string types
7. **Run tests:** Fix any string comparison bugs

---

## Example: Full Flow After Changes

```
1. User creates custom type "EquipmentRequest":
   POST /api/request-definitions/custom-types
   {
     "typeName": "EquipmentRequest",
     "displayName": "Equipment Request",
     "formSchemaJson": {
       "fields": [
         {"name": "equipmentType", "type": "string", "label": "Equipment Type", "isRequired": true},
         {"name": "quantity", "type": "number", "label": "Quantity", "isRequired": true}
       ]
     }
   }

2. Admin creates workflow for EquipmentRequest:
   POST /api/request-definitions
   {
     "requestType": "EquipmentRequest",
     "steps": [{"stepType": 0, "orgNodeId": "...", "sortOrder": 1}]
   }

3. Employee creates equipment request:
   POST /api/requests
   {
     "requestType": "EquipmentRequest",
     "data": {"equipmentType": "Laptop", "quantity": 2}
   }

4. System validates against custom schema, routes through workflow
```