using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record ChangeProductStatusRequest(
    Guid Id,
    string Version,
    ProductStatus Status) : IRequest<ProductDto>;

public sealed class ChangeProductStatusRequestValidator : CustomValidator<ChangeProductStatusRequest>
{
    public ChangeProductStatusRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ChangeProductStatusRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<ChangeProductStatusRequest, ProductDto>
{
    public async Task<ProductDto> Handle(ChangeProductStatusRequest request, CancellationToken ct)
    {
        await work.ExecuteAsync(async () =>
        {
            var (product, type) = await ProductPersistenceHelper.LockProductAsync(db, request.Id, request.Version, ct);
            product.ChangeStatus(request.Status, type);
            product.Validate(type);
            return true;
        }, ct);

        return await ProductDetailReader.ReadProductAsync(db, actor, users, request.Id, false, ct);
    }
}

