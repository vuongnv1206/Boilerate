using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record UpdateProductContentRequest(
    Guid Id,
    string Version,
    string ProductTypeVersion,
    ProductContent Content) : IRequest<ProductDto>;

public sealed class UpdateProductContentRequestValidator : CustomValidator<UpdateProductContentRequest>
{
    public UpdateProductContentRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Version).NotEmpty();
        RuleFor(x => x.ProductTypeVersion).NotEmpty();
        RuleFor(x => x.Content).NotNull().SetValidator(new ProductContentValidator());
    }
}

public sealed class UpdateProductContentRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<UpdateProductContentRequest, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductContentRequest request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.Id, request.Version, ct);
            CommerceConcurrency.CheckVersion(request.ProductTypeVersion, type.Version);
            await ProductPersistenceHelper.ApplyContentAsync(db, actor, product, type, request.Content, ct);
            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.Id, false, ct);
    }
}

