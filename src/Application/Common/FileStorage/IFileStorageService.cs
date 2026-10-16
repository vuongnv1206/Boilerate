using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Common;

namespace Boilerate.Application.Common.FileStorage;

/// <summary>
/// File storage service interface
/// </summary>
public interface IFileStorageService : ITransientService
{
    /// <summary>
    /// Upload file lên storage
    /// </summary>
    /// <typeparam name="T">Entity type (dùng để organize folders)</typeparam>
    Task<string> UploadAsync<T>(
        Microsoft.AspNetCore.Http.IFormFile? file,
        FileType supportedFileType,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Remove file từ storage
    /// </summary>
    void Remove(string? path);

    /// <summary>Open a stored local image; the caller owns and must dispose the stream.</summary>
    Stream OpenRead(string path);
}
