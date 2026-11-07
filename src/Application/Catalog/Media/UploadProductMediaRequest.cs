using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Media;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Media;

public sealed record UploadProductMediaRequest(IFormFile File) : IRequest<MediaDto>;

public sealed class UploadProductMediaRequestValidator : CustomValidator<UploadProductMediaRequest>
{
    public UploadProductMediaRequestValidator()
    {
        RuleFor(x => x.File).NotNull();
    }
}

public sealed class UploadProductMediaRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICommerceMediaStorage storage,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<UploadProductMediaRequest, MediaDto>
{
    public async Task<MediaDto> Handle(UploadProductMediaRequest request, CancellationToken ct)
    {
        string userId = actor.GetUserId().ToString();
        if (!await users.HasPermissionAsync(userId, "Permissions.Product.Create", ct) &&
            !await users.HasPermissionAsync(userId, "Permissions.Product.Update", ct))
        {
            throw new ForbiddenException("Uploading product images requires Product.Create or Product.Update.");
        }

        if (request.File.Length is <= 0 or > 5242880)
        {
            throw new FluentValidation.ValidationException("Images must be 1 byte to 5 MiB.");
        }

        var asset = new MediaAsset(Path.GetExtension(request.File.FileName));
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync(ct); // Durable intent before writing any file.
        return await work.ExecuteAsync(async () =>
        {
            await db.MediaAssets
                .Where(m => m.Id == asset.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Version, m => m.Version), ct);
            asset = await db.MediaAssets.SingleAsync(m => m.Id == asset.Id, ct);

            // Local IFormFile is already buffered by ASP.NET; the short upload lock excludes the TTL cleaner.
            await storage.UploadAsync(request.File, asset.StorageKey, ct);
            using var stream = storage.OpenRead(asset.StorageKey);
            asset.CompleteUpload(stream.Length);
            return new MediaDto(asset.Id, asset.ContentType, asset.Size, asset.State.ToString(), asset.Version);
        }, ct);
    }
}