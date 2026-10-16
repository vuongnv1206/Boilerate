using System.IO;
using System.Collections.Generic;

namespace Boilerate.Application.Common.BlobStorage;

/// <summary>
/// Request to upload blob to storage
/// </summary>
public class UploadBlobRequest
{
    /// <summary>
    /// Container name
    /// </summary>
    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// Blob name (file name with path)
    /// Example: "products/product-123.jpg" or "avatars/user-456.png"
    /// </summary>
    public string BlobName { get; set; } = default!;

    /// <summary>
    /// Content type (MIME type)
    /// Example: "image/jpeg", "application/pdf"
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// File data stream
    /// </summary>
    public Stream Data { get; set; } = default!;

    /// <summary>
    /// Overwrite if blob exists
    /// </summary>
    public bool Overwrite { get; set; } = true;

    /// <summary>
    /// Optional metadata (key-value pairs)
    /// </summary>
    public IDictionary<string, string>? Metadata { get; set; }
}
