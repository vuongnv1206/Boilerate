using System;
using System.Collections.Generic;

namespace Boilerate.Application.Common.BlobStorage;

/// <summary>
/// Blob information model
/// </summary>
public class BlobModel
{
    /// <summary>
    /// Blob name (file name with path)
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Container name
    /// </summary>
    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// Blob size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Content type (MIME type)
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Public URL (if container is public)
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// ETag for concurrency control
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Metadata (key-value pairs)
    /// </summary>
    public IDictionary<string, string>? Metadata { get; set; }
}
