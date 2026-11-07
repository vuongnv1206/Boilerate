using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace Boilerate.Application.Ordering.Orders;

public sealed record ExpireOrdersRequest : IRequest<int>;

public sealed class ExpireOrdersRequestValidator : CustomValidator<ExpireOrdersRequest>
{
}

public sealed class ExpireOrdersRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    IOptions<OrderingSettings> settings,
    TimeProvider clock) : IRequestHandler<ExpireOrdersRequest, int>
{
    public Task<int> Handle(ExpireOrdersRequest request, CancellationToken ct) =>
        OrderStockCoordinator.ExpirePendingOrdersAsync(
            db,
            work,
            settings.Value,
            clock.GetUtcNow().UtcDateTime,
            ct);
}

