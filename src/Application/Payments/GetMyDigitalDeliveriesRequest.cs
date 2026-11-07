using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Payments;

public sealed record GetMyDigitalDeliveriesRequest(Guid OrderId) : IRequest<List<DigitalDeliveryDto>>;

public sealed class GetMyDigitalDeliveriesRequestValidator : CustomValidator<GetMyDigitalDeliveriesRequest>
{
    public GetMyDigitalDeliveriesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetMyDigitalDeliveriesRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<GetMyDigitalDeliveriesRequest, List<DigitalDeliveryDto>>
{
    public Task<List<DigitalDeliveryDto>> Handle(GetMyDigitalDeliveriesRequest request, CancellationToken ct) =>
        DigitalDeliveryReader.ReadDeliveriesAsync(
            db,
            work,
            protection,
            clock.GetUtcNow().UtcDateTime,
            request.OrderId,
            OrderingAuthHelper.RequireCustomer(actor),
            null,
            ct);
}

