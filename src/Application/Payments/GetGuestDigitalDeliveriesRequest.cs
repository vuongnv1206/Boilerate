using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Boilerate.Application.Payments;

public sealed record GetGuestDigitalDeliveriesRequest(
    Guid OrderId,
    string VerifiedEmail) : IRequest<List<DigitalDeliveryDto>>;

public sealed class GetGuestDigitalDeliveriesRequestValidator : CustomValidator<GetGuestDigitalDeliveriesRequest>
{
    public GetGuestDigitalDeliveriesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetGuestDigitalDeliveriesRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    TimeProvider clock,
    IDataProtectionProvider protection) : IRequestHandler<GetGuestDigitalDeliveriesRequest, List<DigitalDeliveryDto>>
{
    public Task<List<DigitalDeliveryDto>> Handle(GetGuestDigitalDeliveriesRequest request, CancellationToken ct) =>
        DigitalDeliveryReader.ReadDeliveriesAsync(
            db,
            work,
            protection,
            clock.GetUtcNow().UtcDateTime,
            request.OrderId,
            null,
            OrderingAuthHelper.NormalizeEmail(request.VerifiedEmail),
            ct);
}

