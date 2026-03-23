namespace HrSystemApp.Application.Common;

/// <summary>
/// Standardized API response envelope
/// </summary>
/// <typeparam name="T">Type of the data payload</typeparam>
public record ApiResponse<T>(
    bool IsSuccess,
    T? Data = default,
    Error? Error = null);

/// <summary>
/// Standardized API response envelope (no data)
/// </summary>
public record ApiResponse(
    bool IsSuccess,
    Error? Error = null);
