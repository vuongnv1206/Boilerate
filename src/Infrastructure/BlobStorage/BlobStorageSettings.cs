using Boilerate.Infrastructure.BlobStorage.Azure;
using Boilerate.Infrastructure.BlobStorage.Aws;

namespace Boilerate.Infrastructure.BlobStorage;

/// <summary>
/// Root Configuration for Blob Storage
/// </summary>
public class BlobStorageSettings
{
    public bool Enabled { get; set; }
    /// <summary>
    /// Storage provider: "Azure" or "AWS"
    /// </summary>
    public string Provider { get; set; } = "Azure";

    /// <summary>
    /// Default container/bucket name
    /// </summary>
    public string DefaultContainer { get; set; } = "default";

    /// <summary>
    /// Enable public access by default
    /// </summary>
    public bool DefaultPublicAccess { get; set; } = false;

    /// <summary>
    /// Azure specific settings
    /// </summary>
    public AzureStorageSettings? Azure { get; set; }

    /// <summary>
    /// AWS specific settings
    /// </summary>
    public AwsStorageSettings? Aws { get; set; }
}
