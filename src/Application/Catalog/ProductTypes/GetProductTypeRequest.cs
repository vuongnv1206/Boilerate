using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Catalog;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Catalog.ProductTypes;

public sealed record GetProductTypeRequest(Guid Id) : IRequest<ProductType>;

public sealed class GetProductTypeRequestValidator : CustomValidator<GetProductTypeRequest>
{
    public GetProductTypeRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class GetProductTypeRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductTypeRequest, ProductType>
{
    public async Task<ProductType> Handle(GetProductTypeRequest request, CancellationToken ct) =>
        await db.ProductTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .ThenInclude(f => f.Choices)
            .AsSplitQuery()
            .SingleOrDefaultAsync(t => t.Id == request.Id, ct)
        ?? throw new NotFoundException("Product type not found.");
}
