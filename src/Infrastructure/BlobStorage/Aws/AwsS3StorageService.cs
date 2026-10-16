using Amazon.S3;
using Amazon.S3.Model;
using Boilerate.Application.Common.BlobStorage;
using Boilerate.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Boilerate.Infrastructure.BlobStorage.Aws;

/// <summary>
/// AWS S3 Storage implementation
/// </summary>
public class AwsS3StorageService : IBlobStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly BlobStorageSettings _settings;
    private readonly ILogger<AwsS3StorageService> _logger;
    private readonly string _bucketName;

    public AwsS3StorageService(
        IAmazonS3 s3Client,
        IOptions<BlobStorageSettings> settings,
        ILogger<AwsS3StorageService> logger)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;

        if (_settings.Aws == null || string.IsNullOrEmpty(_settings.Aws.BucketName))
        {
            throw new InvalidOperationException("AWS S3 settings are not configured.");
        }

        _bucketName = _settings.Aws.BucketName;
    }

    private static bool HasDotSegment(string key) => key.Split('/').Any(segment => segment is "." or "..");

    private string? ObjectUrl(string key)
    {
        // A browser normalizes dot-only segments. Do not return a URL pointing at a different object.
        if (HasDotSegment(key))
        {
            return null;
        }

        string escaped = string.Join("/", key.Split('/').Select(Uri.EscapeDataString));
        return $"https://{_bucketName}.s3.{_settings.Aws!.Region}.amazonaws.com/{escaped}";
    }

    public async Task<string> UploadAsync(UploadBlobRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{request.ContainerName}/{request.BlobName}";
            string url = ObjectUrl(key) ?? throw new FluentValidation.ValidationException("Object names cannot contain '.' or '..' path segments.");

            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = request.Data,
                ContentType = request.ContentType,
                AutoCloseStream = false,
                IfNoneMatch = request.Overwrite ? null : "*"
            };

            if (request.Metadata != null)
            {
                foreach (var kvp in request.Metadata) { putRequest.Metadata.Add(kvp.Key, kvp.Value); }
            }

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
            _logger.LogInformation("Uploaded object '{Key}' to bucket '{BucketName}'", key, _bucketName);

            return url;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is System.Net.HttpStatusCode.PreconditionFailed or System.Net.HttpStatusCode.Conflict)
        {
            throw new ConflictException("The object changed or already exists. Reload before retrying.");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to upload to S3");
            throw new InternalServerException("Failed to upload to S3.", innerException: ex);
        }
    }

    public async Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{containerName}/{blobName}";
            using var response = await _s3Client.GetObjectAsync(_bucketName, key, cancellationToken);
            var memoryStream = new MemoryStream();
            try
            {
                await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                return memoryStream;
            }
            catch
            {
                await memoryStream.DisposeAsync();
                throw;
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"Object '{containerName}/{blobName}' not found.");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to download from S3");
            throw new InternalServerException("Failed to download from S3.", innerException: ex);
        }
    }

    public async Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, $"{containerName}/{blobName}", cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from S3");
            throw new InternalServerException("Failed to delete from S3.", innerException: ex);
        }
    }

    public async Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketName, $"{containerName}/{blobName}", cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence in S3");
            throw new InternalServerException("Unable to check S3 object existence.", innerException: ex);
        }
    }

    public async Task<string> GetBlobUrlAsync(string containerName, string blobName, int expiryMinutes = 60, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = $"{containerName}/{blobName}";
            if (HasDotSegment(key))
            {
                throw new FluentValidation.ValidationException("This object name cannot be represented by a safe HTTP URL.");
            }

            if (!await ExistsAsync(containerName, blobName, cancellationToken)) { throw new NotFoundException($"Object '{key}' not found."); }

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };
            return _s3Client.GetPreSignedURL(request);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to get S3 URL");
            throw new InternalServerException("Failed to get S3 URL.", innerException: ex);
        }
    }

    public async Task<List<BlobModel>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = $"{containerName}/{prefix ?? string.Empty}"
            };

            var blobs = new List<BlobModel>();
            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                foreach (var obj in response.S3Objects)
                {
                    var name = obj.Key.StartsWith($"{containerName}/", StringComparison.Ordinal) ? obj.Key.Substring(containerName.Length + 1) : obj.Key;
                    blobs.Add(new BlobModel
                    {
                        Name = name,
                        ContainerName = containerName,
                        Size = obj.Size,
                        LastModified = obj.LastModified,
                        Url = ObjectUrl(obj.Key),
                        ETag = obj.ETag
                    });
                }
                listRequest.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated);

            return blobs;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list S3 objects");
            throw new InternalServerException("Failed to list S3 objects.", innerException: ex);
        }
    }

    // The configured bucket is shared; a container is a logical prefix, not a separate S3 bucket.
    public Task CreateContainerAsync(string containerName, bool isPublic = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("S3 logical containers cannot be created or given public ACLs through this adapter. Configure bucket policy explicitly.");

    public Task DeleteContainerAsync(string containerName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("S3 logical containers cannot be deleted through this adapter. Delete their objects explicitly.");

    public async Task<List<string>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var listRequest = new ListObjectsV2Request { BucketName = _bucketName, Delimiter = "/" };
            var containers = new List<string>();
            ListObjectsV2Response response;
            do
            {
                response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);
                containers.AddRange(response.CommonPrefixes.Select(p => p.TrimEnd('/')));
                listRequest.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            return containers;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to list S3 containers");
            throw new InternalServerException("Failed to list S3 containers.", innerException: ex);
        }
    }
}
