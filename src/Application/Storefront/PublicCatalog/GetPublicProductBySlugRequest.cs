using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.PublicCatalog;

public sealed record GetPublicProductBySlugRequest(string Slug) : IRequest<ProductDto>;

public sealed class GetPublicProductBySlugRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetPublicProductBySlugRequest, ProductDto>
{
    public async Task<ProductDto> Handle(GetPublicProductBySlugRequest request, CancellationToken ct)
    {
        string slug = CatalogRules.Slug(request.Slug);
        var id = await db.Products
            .Where(p => p.NormalizedSlug == slug && !p.IsDeleted && p.Status == ProductStatus.Published)
            .Select(p => (Guid?)p.Id)
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product not found.");
        return await ProductDetailReader.ReadProductAsync(db, actor, users, id, true, ct);
    }
}

