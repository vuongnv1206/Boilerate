using Microsoft.AspNetCore.Http;

namespace Boilerate.Application.Common.FileStorage;

public interface ICommerceMediaStorage
{
    Task<string> UploadAsync(IFormFile file, string storageKey, CancellationToken ct);
    Stream OpenRead(string storageKey);
}
