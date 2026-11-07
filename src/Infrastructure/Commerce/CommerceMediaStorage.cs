using Boilerate.Application.Common.FileStorage;
using Boilerate.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;

namespace Boilerate.Infrastructure.Commerce;

public sealed class CommerceMediaStorage(LocalFileStorageService storage) : ICommerceMediaStorage
{
    public Task<string> UploadAsync(IFormFile file, string storageKey, CancellationToken ct) =>
        storage.UploadCommerceAsync(file, storageKey, ct);

    public Stream OpenRead(string storageKey) => storage.OpenRead(storageKey);
}
