using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record GetProductRequest(Guid Id) : IRequest<ProductDto>;

public sealed class GetProductRequestValidator : CustomValidator<GetProductRequest>
{
    public GetProductRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetProductRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetProductRequest, ProductDto>
{
    public Task<ProductDto> Handle(GetProductRequest request, CancellationToken ct) =>
        ProductDetailReader.ReadProductAsync(db, actor, users, request.Id, false, ct);
}

