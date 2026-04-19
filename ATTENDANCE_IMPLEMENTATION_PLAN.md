# Attendance Clock-In / Clock-Out — Full Implementation Plan

This document is written for a developer (or cheaper model) to implement step by step.
A senior reviewer will verify each change before merging.

---

## Context & Goals

The system must support:
- **Multiple clock-in/clock-out sessions per day** (employee can clock in, out, in, out… as many times as needed)
- **Night shifts** (e.g. 10 pm – 6 am)
- **Overnight shifts that cross midnight** (e.g. 10 pm Day 1 → 1 am Day 2), where the second clock-in at 12:30 am still belongs to the **Day 1** attendance record
- **Accurate total hours** — only actual worked time, not wall-clock span from first clock-in to last clock-out
- **Session-level detail** exposed in the API so the app can show a timeline of every clock-in/clock-out pair
- **Admin OverrideClockIn** (symmetric with the existing OverrideClockOut)

---

## Part A — Changes Already Applied

The following changes were made in a previous session. **Verify these are in place before continuing:**

| File | What was changed |
|------|-----------------|
| `Application/Interfaces/Repositories/IAttendanceLogRepository.cs` | Added `GetLastClockInAsync(Guid attendanceId)` method |
| `Infrastructure/Repositories/AttendanceLogRepository.cs` | Implemented `GetLastClockInAsync` — queries most recent `ClockIn` log for an attendance |
| `Application/Features/Attendance/Common/AttendanceSummaryCalculator.cs` | `ApplyClockOut` now accepts `sessionStartUtc` and uses `TotalHours +=` (accumulates per session) |
| `Application/Features/Attendance/Commands/ClockIn/ClockInCommand.cs` | `FirstClockInLogId` is only set when `FirstClockInUtc is null`; resets `LastClockOutUtc = null` and `LastClockOutLogId = null` after a second clock-in |
| `Application/Features/Attendance/Commands/ClockOut/ClockOutCommand.cs` | Calls `GetLastClockInAsync` for session start; guard uses session start; passes `sessionStartUtc` to `ApplyClockOut` |
| `Infrastructure/Services/AutoClockOutService.cs` | Calls `GetLastClockInAsync` for session start before calling `ApplyClockOut` |
| `Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs` | Calls `GetLastClockInAsync` for session start; guard uses session start; passes `sessionStartUtc` to `ApplyClockOut` |

> ⚠️ **Known remaining bug in OverrideClockOut**: because `ApplyClockOut` now uses `TotalHours +=`, calling it on an already-completed attendance record double-counts hours. This is fixed in **Step 3** below.

---

## Part B — Steps to Implement

Work through these steps in order. Each step lists the exact files to touch and the full logic.

---

### Step 1 — Fix `ResolveBusinessDateAsync` for Overnight Shifts

**File:** `HrSystemApp.Infrastructure/Services/AttendanceRulesProvider.cs`

**Problem:** The current implementation converts `timestampUtc` to local time and returns the calendar date. For a shift running 10 pm – 1 am, an employee who clocks in at 12:30 am gets assigned `Day 2`, but that session should belong to `Day 1`'s attendance record.

**Rule:**
- If the company's shift is **overnight** (`EndTime <= StartTime`), check whether `timestampUtc` falls inside the **previous calendar day's** shift window `[prevShiftStartUtc, prevShiftEndUtc]`.
- If yes → return `calendarDate - 1 day`.
- If no → return `calendarDate` as normal.
- For normal (non-overnight) shifts this logic is never entered, so behaviour is unchanged.
- The rule also naturally handles the "two shifts, same calendar day" case: once the overnight window closes (past its `ShiftEndUtc`), the 9 am clock-in is after `prevShiftEndUtc`, so the check fails and `calendarDate` (Day 2) is returned correctly.

**Current query in `ResolveBusinessDateAsync`** only fetches `TimeZoneId`. Extend it to also fetch `StartTime` and `EndTime`.

**New implementation:**

```csharp
public async Task<DateOnly> ResolveBusinessDateAsync(
    Guid employeeId, DateTime timestampUtc, CancellationToken cancellationToken = default)
{
    var data = await _context.Employees
        .AsNoTracking()
        .Where(x => x.Id == employeeId)
        .Select(x => new
        {
            x.Company.TimeZoneId,
            x.Company.StartTime,
            x.Company.EndTime
        })
        .FirstOrDefaultAsync(cancellationToken);

    if (data is null || string.IsNullOrWhiteSpace(data.TimeZoneId))
        return DateOnly.FromDateTime(timestampUtc);

    var timeZone = ResolveTimeZone(data.TimeZoneId);
    var localTime = TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc), timeZone);
    var calendarDate = DateOnly.FromDateTime(localTime);

    // For overnight shifts: check if this timestamp still belongs to
    // the PREVIOUS calendar day's shift window.
    bool isOvernightShift = data.EndTime <= data.StartTime;
    if (isOvernightShift)
    {
        var prevDate = calendarDate.AddDays(-1);

        var prevLocalStart = prevDate.ToDateTime(
            TimeOnly.FromTimeSpan(data.StartTime), DateTimeKind.Unspecified);
        var prevLocalEnd   = prevDate.ToDateTime(
            TimeOnly.FromTimeSpan(data.EndTime), DateTimeKind.Unspecified)
            .AddDays(1); // end is on the following calendar day

        var prevShiftStartUtc = TimeZoneInfo.ConvertTimeToUtc(prevLocalStart, timeZone);
        var prevShiftEndUtc   = TimeZoneInfo.ConvertTimeToUtc(prevLocalEnd,   timeZone);

        if (timestampUtc >= prevShiftStartUtc && timestampUtc <= prevShiftEndUtc)
            return prevDate;
    }

    return calendarDate;
}
```

---

### Step 2 — Add `GetByAttendanceIdAsync` to the Log Repository

This method is needed by OverrideClockIn, OverrideClockOut, and the sessions endpoint.

**File:** `HrSystemApp.Application/Interfaces/Repositories/IAttendanceLogRepository.cs`

Add:
```csharp
/// <summary>
/// Returns all logs for the given attendance ordered by TimestampUtc ascending.
/// </summary>
Task<IReadOnlyList<AttendanceLog>> GetByAttendanceIdAsync(
    Guid attendanceId, CancellationToken cancellationToken = default);
```

**File:** `HrSystemApp.Infrastructure/Repositories/AttendanceLogRepository.cs`

Implement:
```csharp
public async Task<IReadOnlyList<AttendanceLog>> GetByAttendanceIdAsync(
    Guid attendanceId, CancellationToken cancellationToken = default)
{
    return await _dbSet
        .Where(x => x.AttendanceId == attendanceId)
        .OrderBy(x => x.TimestampUtc)
        .ToListAsync(cancellationToken);
}
```

---

### Step 3 — Fix `OverrideClockOut` TotalHours Double-Count

**File:** `HrSystemApp.Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs`

**Problem:** `ApplyClockOut` now does `TotalHours +=`. When an admin overrides a *completed* attendance record (one that already has accumulated `TotalHours`), this adds the new session duration on top of the existing total — producing a wrong value.

**Fix:** After saving the new log, reload all logs for the attendance and recalculate `TotalHours` from scratch using `AttendanceSummaryCalculator.CalculateTotalHoursFromLogs` (added in Step 4).

**Replace** the block that calls `ApplyClockOut` with:

```csharp
attendance.LastClockOutLogId = log.Id;
attendance.LastClockOutUtc   = normalizedClockOut;
attendance.IsEarlyLeave      = normalizedClockOut < rules.ShiftEndUtc;
attendance.Reason            = request.Reason;

// Recalculate TotalHours from ALL logs so that overriding one session
// does not double-count hours that were already accumulated.
var allLogs = await _unitOfWork.AttendanceLogs
    .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
attendance.TotalHours = AttendanceSummaryCalculator.CalculateTotalHoursFromLogs(allLogs);

attendance.Status = AttendanceSummaryCalculator.ResolveStatus(
    attendance.IsLate, attendance.IsEarlyLeave, hasClockOut: true);

await _unitOfWork.Attendances.UpdateAsync(attendance, cancellationToken);
```

Remove the old `ApplyClockOut(...)` call. `BuildSnapshot(attendance)` for the `AfterSnapshotJson` must be called **after** the fields above are set.

---

### Step 4 — Add `CalculateTotalHoursFromLogs` and `BuildSessions` to `AttendanceSummaryCalculator`

**File:** `HrSystemApp.Application/Features/Attendance/Common/AttendanceSummaryCalculator.cs`

Also add the using for the DTO at the top:
```csharp
using HrSystemApp.Application.DTOs.Attendance;
```

#### 4a — `CalculateTotalHoursFromLogs`

Pairs ClockIn/ClockOut logs in chronological order and sums actual worked durations.

```csharp
/// <summary>
/// Calculates total worked hours from a set of attendance logs by pairing
/// ClockIn and ClockOut entries chronologically.
/// An unpaired ClockIn (open session) contributes zero hours.
/// </summary>
public static decimal CalculateTotalHoursFromLogs(IReadOnlyList<AttendanceLog> logs)
{
    var clockIns  = logs
        .Where(l => l.Type == AttendanceLogType.ClockIn)
        .OrderBy(l => l.TimestampUtc)
        .ToList();
    var clockOuts = logs
        .Where(l => l.Type == AttendanceLogType.ClockOut)
        .OrderBy(l => l.TimestampUtc)
        .ToList();

    decimal total = 0m;
    int pairs = Math.Min(clockIns.Count, clockOuts.Count);
    for (int i = 0; i < pairs; i++)
    {
        var duration = (clockOuts[i].TimestampUtc - clockIns[i].TimestampUtc).TotalHours;
        total += Math.Round((decimal)Math.Max(duration, 0), 2);
    }
    return Math.Round(total, 2);
}
```

#### 4b — `BuildSessions`

Converts the same log set into a structured list of session pairs for the API.

```csharp
/// <summary>
/// Builds a list of clock-in/clock-out session pairs from attendance logs.
/// The last session will have SessionEndUtc = null if the employee is still clocked in.
/// </summary>
public static IReadOnlyList<AttendanceSessionDto> BuildSessions(IReadOnlyList<AttendanceLog> logs)
{
    var clockIns  = logs
        .Where(l => l.Type == AttendanceLogType.ClockIn)
        .OrderBy(l => l.TimestampUtc)
        .ToList();
    var clockOuts = logs
        .Where(l => l.Type == AttendanceLogType.ClockOut)
        .OrderBy(l => l.TimestampUtc)
        .ToList();

    var sessions = new List<AttendanceSessionDto>(clockIns.Count);
    for (int i = 0; i < clockIns.Count; i++)
    {
        var start = clockIns[i].TimestampUtc;
        DateTime? end = i < clockOuts.Count ? clockOuts[i].TimestampUtc : null;
        decimal? duration = end.HasValue
            ? Math.Round((decimal)Math.Max((end.Value - start).TotalHours, 0), 2)
            : null;

        sessions.Add(new AttendanceSessionDto(start, end, duration));
    }
    return sessions;
}
```

Also fix the **dead-code branch** in `ResolveStatus` while you are in this file:

```csharp
// BEFORE (redundant first branch):
if (isLate && isEarlyLeave) return AttendanceStatus.Late;
if (isLate)                 return AttendanceStatus.Late;

// AFTER (collapsed):
if (isLate) return AttendanceStatus.Late;
```

---

### Step 5 — Add `AttendanceSessionDto`

**File (new):** `HrSystemApp.Application/DTOs/Attendance/AttendanceSessionDto.cs`

```csharp
namespace HrSystemApp.Application.DTOs.Attendance;

/// <summary>
/// Represents a single worked session within an attendance day
/// (one ClockIn paired with its corresponding ClockOut).
/// SessionEndUtc is null when the session is still open.
/// </summary>
public sealed record AttendanceSessionDto(
    DateTime SessionStartUtc,
    DateTime? SessionEndUtc,
    decimal? DurationHours);
```

---

### Step 6 — Update `AttendanceResponse` and `AttendanceSummaryResponse` to Include Sessions

**File:** `HrSystemApp.Application/DTOs/Attendance/AttendanceResponse.cs`

Replace the record with:
```csharp
namespace HrSystemApp.Application.DTOs.Attendance;

public sealed record AttendanceResponse(
    Guid AttendanceId,
    Guid EmployeeId,
    DateOnly Date,
    DateTime? FirstClockInUtc,
    DateTime? LastClockOutUtc,
    decimal TotalHours,
    string Status,
    bool IsLate,
    bool IsEarlyLeave,
    string? Reason,
    IReadOnlyList<AttendanceSessionDto> Sessions);
```

**File:** `HrSystemApp.Application/DTOs/Attendance/AttendanceSummaryResponse.cs`

Replace the record with:
```csharp
namespace HrSystemApp.Application.DTOs.Attendance;

public sealed record AttendanceSummaryResponse(
    Guid EmployeeId,
    string EmployeeName,
    DateOnly Date,
    DateTime? FirstClockInUtc,
    DateTime? LastClockOutUtc,
    decimal TotalHours,
    string Status,
    bool IsLate,
    bool IsEarlyLeave,
    string? Reason,
    IReadOnlyList<AttendanceSessionDto> Sessions);
```

---

### Step 7 — Update `ClockInCommand` and `ClockOutCommand` to Return Sessions

Both commands must load the attendance logs after committing the transaction and build the sessions list.

**File:** `HrSystemApp.Application/Features/Attendance/Commands/ClockIn/ClockInCommand.cs`

After `await _unitOfWork.CommitTransactionAsync(cancellationToken);` and before the `return Result.Success(...)`:

```csharp
var logs = await _unitOfWork.AttendanceLogs
    .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
var sessions = AttendanceSummaryCalculator.BuildSessions(logs);
```

Update the `return Result.Success(new AttendanceResponse(...))` to pass `sessions` as the last argument.

**File:** `HrSystemApp.Application/Features/Attendance/Commands/ClockOut/ClockOutCommand.cs`

Same change — load logs and build sessions after `CommitTransactionAsync`, pass `sessions` to the response.

**Also update `OverrideClockOutCommand`** and the new `OverrideClockInCommand` (Step 11) to do the same.

---

### Step 8 — Update `GetMyAttendancePagedAsync` to Include `Logs`

**File:** `HrSystemApp.Infrastructure/Repositories/AttendanceRepository.cs`

In `GetMyAttendancePagedAsync`, add `.Include(x => x.Logs)` to the query:

```csharp
var query = _dbSet.AsNoTracking()
    .Include(x => x.Logs)   // ← ADD THIS
    .Where(x => x.EmployeeId == employeeId && x.Date >= fromDate && x.Date <= toDate);
```

The `Logs` navigation property already exists on the `Attendance` entity as `ICollection<AttendanceLog>`.

**Do NOT add `.Include(x => x.Logs)` to `GetCompanyAttendancePagedAsync`** — the company-wide list is kept lean (no session detail) to avoid loading potentially thousands of log rows. HR can drill into sessions via the dedicated endpoint in Step 10.

---

### Step 9 — Update `GetMyAttendanceQueryHandler` to Map Sessions

**File:** `HrSystemApp.Application/Features/Attendance/Queries/GetMyAttendance/GetMyAttendanceQuery.cs`

In the handler, the `items` projection currently builds `AttendanceSummaryResponse` manually. Update it to also build sessions:

```csharp
var items = paged.Items.Select(a => new AttendanceSummaryResponse(
    a.EmployeeId,
    employee.FullName,
    a.Date,
    a.FirstClockInUtc,
    a.LastClockOutUtc,
    a.TotalHours,
    a.Status.ToString(),
    a.IsLate,
    a.IsEarlyLeave,
    a.Reason,
    AttendanceSummaryCalculator.BuildSessions(a.Logs.OrderBy(l => l.TimestampUtc).ToList())
)).ToList();
```

---

### Step 10 — Update `GetCompanyAttendanceQueryHandler` and Mapping Register

The company-wide list does **not** include sessions (kept lean). Pass an empty list for the new `Sessions` field.

**File:** `HrSystemApp.Application/Features/Attendance/Queries/GetCompanyAttendance/GetCompanyAttendanceQuery.cs`

The handler currently uses `paged.Items.Adapt<List<AttendanceSummaryResponse>>()`.
Because `AttendanceSummaryResponse` now has a `Sessions` field, Mapster needs to be told what to put there.

**File:** `HrSystemApp.Application/Mappings/AttendanceMappingRegister.cs`

Update:
```csharp
config.NewConfig<Attendance, AttendanceSummaryResponse>()
    .Map(dest => dest.EmployeeId,     src => src.EmployeeId)
    .Map(dest => dest.EmployeeName,   src => src.Employee != null ? src.Employee.FullName : string.Empty)
    .Map(dest => dest.Date,           src => src.Date)
    .Map(dest => dest.FirstClockInUtc,  src => src.FirstClockInUtc)
    .Map(dest => dest.LastClockOutUtc,  src => src.LastClockOutUtc)
    .Map(dest => dest.TotalHours,     src => src.TotalHours)
    .Map(dest => dest.Status,         src => src.Status.ToString())
    .Map(dest => dest.IsLate,         src => src.IsLate)
    .Map(dest => dest.IsEarlyLeave,   src => src.IsEarlyLeave)
    .Map(dest => dest.Reason,         src => src.Reason)
    // Company list is lean — sessions are loaded on demand via the /sessions endpoint.
    .Map(dest => dest.Sessions,       src => new List<AttendanceSessionDto>());
```

---

### Step 11 — Add Dedicated Sessions Endpoint

This allows an employee or HR user to fetch the full timeline for one specific attendance day.

#### 11a — Query Handler (new file)

**File (new):** `HrSystemApp.Application/Features/Attendance/Queries/GetAttendanceSessions/GetAttendanceSessionsQuery.cs`

```csharp
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Attendance;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Features.Attendance.Common;
using HrSystemApp.Application.Interfaces;
using MediatR;

namespace HrSystemApp.Application.Features.Attendance.Queries.GetAttendanceSessions;

public sealed record GetAttendanceSessionsQuery(Guid AttendanceId)
    : IRequest<Result<IReadOnlyList<AttendanceSessionDto>>>;

public class GetAttendanceSessionsQueryHandler
    : IRequestHandler<GetAttendanceSessionsQuery, Result<IReadOnlyList<AttendanceSessionDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetAttendanceSessionsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<AttendanceSessionDto>>> Handle(
        GetAttendanceSessionsQuery request, CancellationToken cancellationToken)
    {
        // Verify the attendance record exists
        var attendance = await _unitOfWork.Attendances
            .GetByIdAsync(request.AttendanceId, cancellationToken);
        if (attendance is null)
            return Result.Failure<IReadOnlyList<AttendanceSessionDto>>(DomainErrors.Attendance.NotFound);

        // Employees can only view their own; HR/Admin can view any.
        // Authorization is enforced at the controller level via [Authorize] roles.

        var logs = await _unitOfWork.AttendanceLogs
            .GetByAttendanceIdAsync(request.AttendanceId, cancellationToken);

        return Result.Success(AttendanceSummaryCalculator.BuildSessions(logs));
    }
}
```

> Note: `GetByIdAsync` is assumed to exist on the base `IRepository<T>`. If it doesn't, add it, or use `GetByEmployeeAndDateAsync` after loading the attendance from the repository.

#### 11b — Controller Endpoint

**File:** `HrSystemApp.Api/Controllers/AttendanceController.cs`

Add to the controller (no additional role restriction — any authenticated user can see their own; HR can see any):

```csharp
[HttpGet("{attendanceId:guid}/sessions")]
public async Task<IActionResult> GetAttendanceSessions(
    Guid attendanceId, CancellationToken cancellationToken)
{
    var result = await _sender.Send(
        new GetAttendanceSessionsQuery(attendanceId), cancellationToken);
    return HandleResult(result);
}
```

---

### Step 12 — Add `OverrideClockInCommand`

**File (new):** `HrSystemApp.Application/Features/Attendance/Commands/OverrideClockIn/OverrideClockInCommand.cs`

Full handler logic:

1. Validate `Reason` is not empty (return `DomainErrors.Attendance.OverrideReasonRequired`).
2. Validate `ClockInUtc` is not in the future (`> DateTime.UtcNow + 5 min` → return `DomainErrors.Attendance.InvalidClockOut` or add a new `InvalidClockIn` error).
3. Normalize: `normalizedClockIn = DateTime.SpecifyKind(request.ClockInUtc, DateTimeKind.Utc)`.
4. Get rules: `rules = await _attendanceRulesProvider.GetRulesAsync(request.EmployeeId, request.Date)`.
5. Fetch `attendance = await _unitOfWork.Attendances.GetByEmployeeAndDateAsync(request.EmployeeId, request.Date)`.
6. If `attendance is null`: create a new `Attendance` record (employee never clocked in on that day — admin is inserting a corrective record). Set `EmployeeId` and `Date`. Add and `SaveChangesAsync`.
7. `beforeSnapshot = BuildSnapshot(attendance)`.
8. Begin transaction.
9. Create `AttendanceLog`:
   - `Type = AttendanceLogType.ClockIn`
   - `Source = AttendanceLogSource.Admin`
   - `TimestampUtc = normalizedClockIn`
   - `Reason = request.Reason`
   - `CreatedAtUtc = DateTime.UtcNow`
10. Add log, `SaveChangesAsync`.
11. Force-set:
    ```csharp
    attendance.FirstClockInUtc   = normalizedClockIn;
    attendance.FirstClockInLogId = log.Id;
    ```
    *(Admin override means the provided time is the authoritative first clock-in, regardless of what was recorded before.)*
12. Recalculate `IsLate`:
    ```csharp
    attendance.IsLate = normalizedClockIn > rules.LateThresholdUtc;
    ```
13. If `attendance.LastClockOutUtc is not null` (attendance is already completed), recalculate TotalHours from all logs:
    ```csharp
    var allLogs = await _unitOfWork.AttendanceLogs
        .GetByAttendanceIdAsync(attendance.Id, cancellationToken);
    attendance.TotalHours = AttendanceSummaryCalculator.CalculateTotalHoursFromLogs(allLogs);
    ```
14. Recalculate `Status`:
    ```csharp
    attendance.Status = AttendanceSummaryCalculator.ResolveStatus(
        attendance.IsLate, attendance.IsEarlyLeave,
        attendance.LastClockOutUtc is not null);
    ```
15. `UpdateAsync(attendance)`, `SaveChangesAsync`.
16. Create `AttendanceAdjustment`:
    ```csharp
    new AttendanceAdjustment {
        AttendanceId       = attendance.Id,
        EmployeeId         = attendance.EmployeeId,
        Reason             = request.Reason,
        UpdatedByUserId    = _currentUserService.UserId ?? "system",
        BeforeSnapshotJson = beforeSnapshot,
        AfterSnapshotJson  = BuildSnapshot(attendance),
        UpdatedAtUtc       = DateTime.UtcNow
    }
    ```
17. Add adjustment, `SaveChangesAsync`.
18. `CommitTransactionAsync`.
19. Load logs and build sessions for the response.
20. Return `Result.Success(new AttendanceResponse(..., sessions))`.

Add a `BuildSnapshot` private method identical to the one in `OverrideClockOutCommand`.

---

### Step 13 — Register `OverrideClockIn` in the Controller

**File:** `HrSystemApp.Api/Controllers/AttendanceController.cs`

Add the request record alongside the existing ones at the bottom of the file:
```csharp
public sealed record OverrideClockInRequest(
    Guid EmployeeId, DateOnly Date, DateTime ClockInUtc, string Reason);
```

Add the endpoint:
```csharp
[HttpPost("admin/override-clock-in")]
[Authorize(Roles = Roles.HrOrAbove)]
public async Task<IActionResult> OverrideClockIn(
    [FromBody] OverrideClockInRequest request, CancellationToken cancellationToken)
{
    var result = await _sender.Send(
        new OverrideClockInCommand(
            request.EmployeeId, request.Date, request.ClockInUtc, request.Reason),
        cancellationToken);
    return HandleResult(result);
}
```

---

## Summary of All Files Changed / Created

| # | Action | File path |
|---|--------|-----------|
| A1 | Verify | `Application/Interfaces/Repositories/IAttendanceLogRepository.cs` |
| A2 | Verify | `Infrastructure/Repositories/AttendanceLogRepository.cs` |
| A3 | Verify | `Application/Features/Attendance/Common/AttendanceSummaryCalculator.cs` |
| A4 | Verify | `Application/Features/Attendance/Commands/ClockIn/ClockInCommand.cs` |
| A5 | Verify | `Application/Features/Attendance/Commands/ClockOut/ClockOutCommand.cs` |
| A6 | Verify | `Infrastructure/Services/AutoClockOutService.cs` |
| A7 | Verify | `Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs` |
| 1 | Edit | `Infrastructure/Services/AttendanceRulesProvider.cs` |
| 2 | Edit | `Application/Interfaces/Repositories/IAttendanceLogRepository.cs` |
| 2 | Edit | `Infrastructure/Repositories/AttendanceLogRepository.cs` |
| 3 | Edit | `Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs` |
| 4 | Edit | `Application/Features/Attendance/Common/AttendanceSummaryCalculator.cs` |
| 5 | **New** | `Application/DTOs/Attendance/AttendanceSessionDto.cs` |
| 6 | Edit | `Application/DTOs/Attendance/AttendanceResponse.cs` |
| 6 | Edit | `Application/DTOs/Attendance/AttendanceSummaryResponse.cs` |
| 7 | Edit | `Application/Features/Attendance/Commands/ClockIn/ClockInCommand.cs` |
| 7 | Edit | `Application/Features/Attendance/Commands/ClockOut/ClockOutCommand.cs` |
| 7 | Edit | `Application/Features/Attendance/Commands/OverrideClockOut/OverrideClockOutCommand.cs` |
| 8 | Edit | `Infrastructure/Repositories/AttendanceRepository.cs` |
| 9 | Edit | `Application/Features/Attendance/Queries/GetMyAttendance/GetMyAttendanceQuery.cs` |
| 10 | Edit | `Application/Mappings/AttendanceMappingRegister.cs` |
| 11 | **New** | `Application/Features/Attendance/Queries/GetAttendanceSessions/GetAttendanceSessionsQuery.cs` |
| 11 | Edit | `Api/Controllers/AttendanceController.cs` |
| 12 | **New** | `Application/Features/Attendance/Commands/OverrideClockIn/OverrideClockInCommand.cs` |
| 13 | Edit | `Api/Controllers/AttendanceController.cs` |

---

## Edge Cases the Reviewer Must Verify

1. **Single-session day** — employee clocks in once and out once. Sessions list has exactly one entry. TotalHours equals session duration.

2. **Multiple sessions, same day** — employee clocks in at 9 am, out at 12 pm, back in at 1 pm, out at 5 pm. TotalHours = 7 h (not 8 h). Sessions list has two entries.

3. **Open session (currently clocked in)** — the last session in the list has `SessionEndUtc = null` and `DurationHours = null`.

4. **Normal overnight shift** — shift is 10 pm – 1 am. Employee clocks in at 10:30 pm (Day 1). `ResolveBusinessDateAsync` returns Day 1. ✓

5. **Cross-midnight session** — same shift. Employee clocks in at 10:30 pm (Day 1), clocks out at 12:45 am (Day 2). Business date is Day 1. `ClockOutCommand` uses `GetOpenAttendanceAsync` which finds the open record regardless of date, so no date logic needed in clock-out. ✓

6. **Second session of overnight shift crossing midnight** — employee clocks out at midnight, then clocks back in at 12:30 am. `ResolveBusinessDateAsync` returns Day 1 (12:30 am is within the 10 pm – 1 am window of Day 1). Second session attaches to the Day 1 record. ✓

7. **New shift after overnight** — employee's overnight shift ends at 1 am. They clock in again at 9 am for a regular Day 2 shift. `ResolveBusinessDateAsync` checks if 9 am is within Day 1's overnight window (10 pm – 1 am). 9 am is after 1 am, so the check fails → returns Day 2. New record created for Day 2. ✓

8. **Admin OverrideClockIn on a date with no existing attendance** — a new attendance record is created, `FirstClockInUtc` is set, status is computed. `TotalHours` stays 0 (no clock-out yet). ✓

9. **Admin OverrideClockIn on a date that already has clock-out** — TotalHours is recalculated from all logs using `CalculateTotalHoursFromLogs`. ✓

10. **Admin OverrideClockOut on completed attendance** — TotalHours is recalculated from all logs. No double-counting. ✓

11. **Auto clock-out on overnight shift** — shift ends at 1 am. Auto-clockout job runs at 1 am or 2 am (hourly). It loads incomplete attendances (any with open session), gets rules for Day 1, confirms `ShiftEndUtc <= nowUtc`, then auto clocks out at `ShiftEndUtc = 1 am`. Idempotency key prevents duplicate processing. ✓

12. **`BuildSessions` with uneven logs** — e.g. two ClockIn logs and one ClockOut log (employee re-clocked-in after a break, hasn't clocked out yet). The loop iterates `Math.Min(2, 1) = 1` pairs. First session gets start+end. Second session: there is no matching ClockOut, so it should appear as open. The loop only covers matched pairs; the open trailing ClockIn is handled by the code outside the loop (the `for` loop only iterates `pairs` times, but the outer loop `for i < clockIns.Count` should also add the trailing open session). Make sure the `BuildSessions` loop iterates over **all** `clockIns.Count` entries, not just `pairs`.

> **Fix for edge case 12:** In `BuildSessions`, change the loop upper bound to `clockIns.Count` (not `pairs`):
> ```csharp
> for (int i = 0; i < clockIns.Count; i++)
> {
>     var start = clockIns[i].TimestampUtc;
>     DateTime? end = i < clockOuts.Count ? clockOuts[i].TimestampUtc : null;
>     // ...
> }
> ```
> This is already shown correctly in Step 4b above. Just confirming the reviewer checks it.
