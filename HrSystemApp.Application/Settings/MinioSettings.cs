namespace HrSystemApp.Application.Settings;

/// <summary>
/// MinIO / S3-compatible object storage configuration.
/// </summary>
public class MinioSettings
{
    /// <summary>
    /// Host and port, e.g. localhost:9000 or minio:9000 (no scheme).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    /// <summary>
    /// Optional region for AWS S3 compatibility; often empty for MinIO.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Optional default bucket for app-wide uploads (not required for multi-bucket API).
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// When true, uploads create the bucket if it does not exist.
    /// </summary>
    public bool AutoCreateBucket { get; set; } = true;
}
