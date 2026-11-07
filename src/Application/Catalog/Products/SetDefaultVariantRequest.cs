using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.Products;

public sealed record SetDefaultVariantRequest(
    Guid ProductId,
    Guid VariantId,
    string Version) : IRequest<ProductDto>;

public sealed class SetDefaultVariantRequestValidator : CustomValidator<SetDefaultVariantRequest>
{
    public SetDefaultVariantRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
    }
}

public sealed class SetDefaultVariantRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<SetDefaultVariantRequest, ProductDto>
{
    public async Task<ProductDto> Handle(SetDefaultVariantRequest request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.ProductId, request.Version, ct);
            var selected = product.GetVariant(request.VariantId);
            if (!selected.IsActive)
            {
                throw new DomainException("Default SKU must be active.");
            }

            // Clear the old flag first to satisfy the filtered unique index during the switch.
            await db.ProductVariants
                .Where(v => v.ProductId == product.Id && v.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsDefault, false), ct);
            product.SelectDefault(request.VariantId);
            foreach (var variant in product.Variants)
            {
                db.Entry(variant).Property(x => x.IsDefault).IsModified = true;
            }

            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.ProductId, false, ct);
    }
}

