using Boilerate.Application.Catalog.Products;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Storefront.PublicCatalog;

public sealed record SearchPublicProductsRequest(ProductSearch Filter) : IRequest<Page<ProductSummary>>;

public sealed class SearchPublicProductsRequestValidator : CustomValidator<SearchPublicProductsRequest>
{
    public SearchPublicProductsRequestValidator()
    {
        RuleFor(x => x.Filter).NotNull();
    }
}

public sealed class SearchPublicProductsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<SearchPublicProductsRequest, Page<ProductSummary>>
{
    public Task<Page<ProductSummary>> Handle(SearchPublicProductsRequest request, CancellationToken ct) =>
        ProductSearchHelper.SearchAsync(db, request.Filter, true, ct);
}

