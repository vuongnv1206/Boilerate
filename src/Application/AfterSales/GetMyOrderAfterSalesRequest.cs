using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.AfterSales;

public sealed record GetMyOrderAfterSalesRequest(Guid OrderId) : IRequest<AfterSalesDto>;

public sealed class GetMyOrderAfterSalesRequestValidator : CustomValidator<GetMyOrderAfterSalesRequest>
{
    public GetMyOrderAfterSalesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetMyOrderAfterSalesRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor) : IRequestHandler<GetMyOrderAfterSalesRequest, AfterSalesDto>
{
    public Task<AfterSalesDto> Handle(GetMyOrderAfterSalesRequest request, CancellationToken ct) =>
        AfterSalesHelper.ReadSummaryAsync(
            db,
            work,
            request.OrderId,
            OrderingAuthHelper.RequireCustomer(actor),
            ct);
}

