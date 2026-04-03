using HrSystemApp.Api.Authorization;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.Errors;
using HrSystemApp.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrSystemApp.Api.Controllers;

/// <summary>
/// MinIO object storage (admin).
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = Roles.CompanyAdmins)]
public class MinioController : BaseApiController
{
    private readonly IMinioService _minioService;

    public MinioController(IMinioService minioService)
    {
        _minioService = minioService;
    }

    /// <summary>Upload a file to a bucket (optional prefix folder).</summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string bucketName,
        [FromQuery] string objectName,
        [FromQuery] string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ApiResponse<object>(false, null,
                DomainErrors.General.ValidationError with { Message = "No file was uploaded." }));

        await using var stream = file.OpenReadStream();
        var result = await _minioService.UploadAsync(
            stream,
            file.Length,
            file.ContentType ?? "application/octet-stream",
            bucketName,
            objectName,
            prefix,
            cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Get a presigned download URL for an object.</summary>
    [HttpGet("get-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUrl(
        [FromQuery] string bucketName,
        [FromQuery] string objectName,
        [FromQuery] int expirySeconds = 86400,
        CancellationToken cancellationToken = default)
    {
        var result = await _minioService.GetPresignedUrlAsync(bucketName, objectName, expirySeconds, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Delete an object.</summary>
    [HttpDelete("delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromQuery] string bucketName,
        [FromQuery] string objectName,
        CancellationToken cancellationToken = default)
    {
        var result = await _minioService.DeleteObjectAsync(bucketName, objectName, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>List object keys under a prefix.</summary>
    [HttpGet("list-objects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListObjects(
        [FromQuery] string bucketName,
        [FromQuery] string? prefix = null,
        [FromQuery] bool recursive = true,
        [FromQuery] bool versions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _minioService.ListObjectsAsync(bucketName, prefix, recursive, versions, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>Check whether a bucket exists.</summary>
    [HttpGet("bucket-exists")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BucketExists(
        [FromQuery] string bucketName,
        CancellationToken cancellationToken = default)
    {
        var result = await _minioService.BucketExistsAsync(bucketName, cancellationToken);
        return HandleResult(result);
    }
}
