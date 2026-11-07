using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Media;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Media;

public sealed record OpenProductMediaRequest(Guid Id, bool PublishedOnly = false) : IRequest<OpenedMediaDto>;

public sealed class OpenProductMediaRequestValidator : CustomValidator<OpenProductMediaRequest>
{
    public OpenProductMediaRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class OpenProductMediaRequestHandler(
    IApplicationDbContext db,
    ICommerceMediaStorage storage) : IRequestHandler<OpenProductMediaRequest, OpenedMediaDto>
{
    public async Task<OpenedMediaDto> Handle(OpenProductMediaRequest request, CancellationToken ct)
    {
        var asset = await db.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                m => m.Id == request.Id && m.State == MediaState.Ready &&
                    (!request.PublishedOnly || db.Set<ProductMedia>().Any(link => link.MediaAssetId == m.Id &&
                        db.Products.Any(p => p.Id == link.ProductId && p.Status == ProductStatus.Published && !p.IsDeleted))),
                ct)
            ?? throw new NotFoundException("Media not found.");
        try
        {
            return new(storage.OpenRead(asset.StorageKey), asset.ContentType);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Media not found.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new NotFoundException("Media not found.");
        }
    }
}
