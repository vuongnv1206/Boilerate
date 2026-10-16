using System.ComponentModel;

namespace Boilerate.Domain.Common;

/// <summary>
/// Supported file types với extensions whitelist
/// </summary>
public enum FileType
{
    /// <summary>
    /// Image files: .jpg, .png, .jpeg
    /// </summary>
    [Description(".jpg,.png,.jpeg")]
    Image
}
