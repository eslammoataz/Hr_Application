# Schema Per Request - Implemented

## Approach

Each request captures its own schema at creation time. This ensures schema changes to the RequestType don't affect existing requests.

## Implementation

### Request Entity Changes

**Before:**
```csharp
public int SchemaVersion { get; set; } = 1;
```

**After:**
```csharp
public string? CapturedSchemaJson { get; set; }
```

### CreateRequestCommandHandler

When a request is created, the schema is captured:

```csharp
var newRequest = new Request
{
    // ...
    CapturedSchemaJson = requestType.FormSchemaJson,  // Schema captured at creation
    // ...
};
```

## How It Works

1. **Creation**: When a request is created, `CapturedSchemaJson` is set to the current `RequestType.FormSchemaJson`
2. **Independence**: The request now contains its own schema - changes to `RequestType` don't affect existing requests
3. **Audit**: `DynamicDataJson` + `CapturedSchemaJson` together preserve the exact state at creation

## Database Migration Required

The `Request` table needs a new column:
```sql
ALTER TABLE "Requests" ADD COLUMN "CapturedSchemaJson" nvarchar(max);
```

## Status: ✅ Implemented

This approach is simpler than schema versioning with snapshots and provides the same benefit for the common case where you don't need to track schema history.
