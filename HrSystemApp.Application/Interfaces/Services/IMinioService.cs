using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Storage;

namespace HrSystemApp.Application.Interfaces.Services;

/// <summary>
/// Object storage operations (MinIO / S3-compatible).
/// </summary>
public interface IMinioService
{
    Task<Result<MinioUploadResult>> UploadAsync(
        Stream stream,
        long size,
        string contentType,
        string bucketName,
        string objectName,
        string? prefix,
        CancellationToken cancellationToken = default);

    Task<Result<string>> GetPresignedUrlAsync(
        string bucketName,
        string objectName,
        int expirySeconds,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> ListObjectsAsync(
        string bucketName,
        string? prefix,
        bool recursive,
        bool versions,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> BucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);
}
