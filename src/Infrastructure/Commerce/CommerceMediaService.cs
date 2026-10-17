using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Media;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
namespace Boilerate.Infrastructure.Commerce;

public sealed class CommerceMediaService(ApplicationDbContext db, CommerceUnitOfWork work, LocalFileStorageService storage) : ICommerceMediaService
{
    public async Task<MediaDto> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 5242880)
            throw new FluentValidation.ValidationException("Images must be 1 byte to 5 MiB.");
        var asset = new MediaAsset(Path.GetExtension(file.FileName));
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct); // Durable intent before writing any file.
        return await work.ExecuteAsync(async () =>
        {
            await db.MediaAssets.Where(m => m.Id == asset.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct);
            asset = await db.MediaAssets.SingleAsync(m => m.Id == asset.Id, ct);
            // Local IFormFile is already buffered by ASP.NET; the short upload lock excludes the TTL cleaner.
            await storage.UploadCommerceAsync(file, asset.StorageKey, ct);
            using var stream = storage.OpenRead(asset.StorageKey);
            asset.CompleteUpload(stream.Length);
            return new MediaDto(asset.Id, asset.ContentType, asset.Size, asset.State.ToString(), asset.Version);
        }, ct);
    }
    public async Task<(Stream Stream, string ContentType)> OpenAsync(Guid id, bool publishedOnly, CancellationToken ct)
    {
        var asset = await db.MediaAssets.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id && m.State == MediaState.Ready &&
            (!publishedOnly || db.Set<ProductMedia>().Any(link => link.MediaAssetId == m.Id && db.Products.Any(p => p.Id == link.ProductId && p.Status == ProductStatus.Published))), ct)
            ?? throw new NotFoundException("Media not found.");
        try
        {
            return (storage.OpenRead(asset.StorageKey), asset.ContentType);
        }
        catch (FileNotFoundException) { throw new NotFoundException("Media not found."); }
        catch (DirectoryNotFoundException) { throw new NotFoundException("Media not found."); }
    }
}