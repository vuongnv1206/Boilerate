using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.Catalog.Products;

public sealed record SearchProductsRequest(ProductSearch Filter) : IRequest<Page<ProductSummary>>;

public sealed class SearchProductsRequestValidator : CustomValidator<SearchProductsRequest>
{
    public SearchProductsRequestValidator()
    {
        RuleFor(x => x.Filter).NotNull();
    }
}

public sealed class SearchProductsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<SearchProductsRequest, Page<ProductSummary>>
{
    public Task<Page<ProductSummary>> Handle(SearchProductsRequest request, CancellationToken ct) =>
        ProductSearchHelper.SearchAsync(db, request.Filter, false, ct);
}

