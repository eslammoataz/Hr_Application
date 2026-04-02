using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Storage;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using HrSystemApp.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace HrSystemApp.Infrastructure.Services;

public class MinioService : IMinioService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioSettings _settings;
    private readonly ILogger<MinioService> _logger;

    public MinioService(
        IMinioClient minioClient,
        IOptions<MinioSettings> settings,
        ILogger<MinioService> logger)
    {
        _minioClient = minioClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<bool>> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new BucketExistsArgs().WithBucket(bucketName);
            var exists = await _minioClient.BucketExistsAsync(args).ConfigureAwait(false);
            return Result.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BucketExistsAsync failed for bucket {Bucket}", bucketName);
            return Result.Failure<bool>(DomainErrors.Storage.ListFailed);
        }
    }

    public async Task<Result<MinioUploadResult>> UploadAsync(
        Stream stream,
        long size,
        string contentType,
        string bucketName,
        string objectName,
        string? prefix,
        CancellationToken cancellationToken = default)
    {
        if (size <= 0)
            return Result.Failure<MinioUploadResult>(DomainErrors.Storage.UploadFailed);

        try
        {
            var exists = await _minioClient
                .BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName))
                .ConfigureAwait(false);

            if (!exists)
            {
                if (!_settings.AutoCreateBucket)
                    return Result.Failure<MinioUploadResult>(DomainErrors.Storage.BucketNotFound);

                await _minioClient
                    .MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName))
                    .ConfigureAwait(false);
            }

            var objectKey = string.IsNullOrEmpty(prefix)
                ? objectName
                : $"{prefix.TrimEnd('/')}/{objectName}";

            var putArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType);

            await _minioClient.PutObjectAsync(putArgs).ConfigureAwait(false);

            var relativePath = $"{bucketName}/{objectKey}";
            return Result.Success(new MinioUploadResult(bucketName, objectKey, relativePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for bucket {Bucket}, object {Object}", bucketName, objectName);
            return Result.Failure<MinioUploadResult>(DomainErrors.Storage.UploadFailed);
        }
    }

    public async Task<Result<string>> GetPresignedUrlAsync(
        string bucketName,
        string objectName,
        int expirySeconds,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _minioClient
                .BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName))
                .ConfigureAwait(false);
            if (!bucketExists)
                return Result.Failure<string>(DomainErrors.Storage.BucketNotFound);

            try
            {
                await _minioClient
                    .StatObjectAsync(new StatObjectArgs().WithBucket(bucketName).WithObject(objectName))
                    .ConfigureAwait(false);
            }
            catch (ObjectNotFoundException)
            {
                return Result.Failure<string>(DomainErrors.Storage.ObjectNotFound);
            }

            var safeExpiry = Math.Clamp(expirySeconds, 60, 60 * 60 * 24 * 7);
            var args = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithExpiry(safeExpiry);

            var url = await _minioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
            return Result.Success(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Presigned URL failed for bucket {Bucket}, object {Object}", bucketName, objectName);
            return Result.Failure<string>(DomainErrors.Storage.PresignedUrlFailed);
        }
    }

    public async Task<Result> DeleteObjectAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _minioClient
                .BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName))
                .ConfigureAwait(false);
            if (!bucketExists)
                return Result.Failure(DomainErrors.Storage.BucketNotFound);

            if (!await ObjectExistsAsync(bucketName, objectName).ConfigureAwait(false))
                return Result.Failure(DomainErrors.Storage.ObjectNotFound);

            await _minioClient
                .RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucketName).WithObject(objectName))
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for bucket {Bucket}, object {Object}", bucketName, objectName);
            return Result.Failure(DomainErrors.Storage.DeleteFailed);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> ListObjectsAsync(
        string bucketName,
        string? prefix,
        bool recursive,
        bool versions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExists = await _minioClient
                .BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName))
                .ConfigureAwait(false);
            if (!bucketExists)
                return Result.Failure<IReadOnlyList<string>>(DomainErrors.Storage.BucketNotFound);

            var listArgs = new ListObjectsArgs()
                .WithBucket(bucketName)
                .WithPrefix(prefix ?? string.Empty)
                .WithRecursive(recursive)
                .WithVersions(versions);

            var objectList = new List<string>();
            await foreach (var item in _minioClient
                               .ListObjectsEnumAsync(listArgs, cancellationToken)
                               .ConfigureAwait(false))
            {
                objectList.Add($"{bucketName}/{item.Key}");
            }

            return Result.Success<IReadOnlyList<string>>(objectList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListObjects failed for bucket {Bucket}", bucketName);
            return Result.Failure<IReadOnlyList<string>>(DomainErrors.Storage.ListFailed);
        }
    }

    private async Task<bool> ObjectExistsAsync(string bucketName, string objectName)
    {
        try
        {
            await _minioClient
                .StatObjectAsync(new StatObjectArgs().WithBucket(bucketName).WithObject(objectName))
                .ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }
}
