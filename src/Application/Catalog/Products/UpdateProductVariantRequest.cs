using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record UpdateProductVariantRequest(
    Guid ProductId,
    Guid VariantId,
    string Version,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsActive,
    Guid? ImageMediaId) : IRequest<ProductDto>;

public sealed class UpdateProductVariantRequestValidator : CustomValidator<UpdateProductVariantRequest>
{
    public UpdateProductVariantRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
    }
}

public sealed class UpdateProductVariantRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<UpdateProductVariantRequest, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductVariantRequest request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.ProductId, request.Version, ct);
            var variant = product.GetVariant(request.VariantId);
            if (variant.Price != request.Price || variant.CompareAtPrice != request.CompareAtPrice)
            {
                await ProductPersistenceHelper.RequirePricePermissionAsync(users, actor, ct);
            }

            product.EditVariant(variant.Id, request.Price, request.CompareAtPrice, request.IsActive, request.ImageMediaId);
            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.ProductId, false, ct);
    }
}

