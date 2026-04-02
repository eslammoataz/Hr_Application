namespace HrSystemApp.Application.DTOs.Storage;

/// <summary>
/// Result of a successful object upload to MinIO.
/// </summary>
public record MinioUploadResult(
    string BucketName,
    string ObjectKey,
    string RelativePath);
