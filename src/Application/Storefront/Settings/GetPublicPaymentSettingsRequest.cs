using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Ordering;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Settings;

public sealed record GetPublicPaymentSettingsRequest : IRequest<GuestPaymentSettingsDto>;

public sealed class GetPublicPaymentSettingsRequestHandler(IApplicationDbContext db)
    : IRequestHandler<GetPublicPaymentSettingsRequest, GuestPaymentSettingsDto>
{
    public async Task<GuestPaymentSettingsDto> Handle(GetPublicPaymentSettingsRequest request, CancellationToken ct)
    {
        var settings = await db.Set<StorefrontPaymentSettings>()
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
        return settings is null || !settings.BankTransferEnabled
            ? new(null, null, null, null, null, false)
            : new(
                settings.BankName,
                settings.AccountNumber,
                settings.AccountHolder,
                settings.Branch,
                settings.Instructions,
                settings.BankTransferEnabled);
    }
}
