using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record DeleteProductRequest(Guid Id, string Version) : IRequest<bool>;

public sealed class DeleteProductRequestValidator : CustomValidator<DeleteProductRequest>
{
    public DeleteProductRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class DeleteProductRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<DeleteProductRequest, bool>
{
    public Task<bool> Handle(DeleteProductRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var (product, _) = await ProductPersistenceHelper.LockProductAsync(db, request.Id, request.Version, ct);
            product.Delete();
            return true;
        }, ct);
}

