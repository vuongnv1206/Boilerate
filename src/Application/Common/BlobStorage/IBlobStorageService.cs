using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Boilerate.Application.Common.BlobStorage;

/// <summary>
/// Service for blob storage operations
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Upload blob to storage
    /// </summary>
    Task<string> UploadAsync(UploadBlobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download blob from storage
    /// </summary>
    Task<Stream> DownloadAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete blob from storage
    /// </summary>
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if blob exists
    /// </summary>
    Task<bool> ExistsAsync(string containerName, string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get blob URL (public or SAS token)
    /// </summary>
    Task<string> GetBlobUrlAsync(string containerName, string blobName, int expiryMinutes = 60, CancellationToken cancellationToken = default);

    /// <summary>
    /// List blobs in container
    /// </summary>
    Task<List<BlobModel>> ListBlobsAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create container if not exists
    /// </summary>
    Task CreateContainerAsync(string containerName, bool isPublic = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete container
    /// </summary>
    Task DeleteContainerAsync(string containerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all containers
    /// </summary>
    Task<List<string>> ListContainersAsync(CancellationToken cancellationToken = default);
}
