using Boilerate.Application.Common.Persistence;
using Boilerate.Domain.Ordering;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Settings;

public sealed record GetAdminStorefrontSettingsRequest : IRequest<AdminStorefrontSettingsDto>;

public sealed class GetAdminStorefrontSettingsRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<GetAdminStorefrontSettingsRequest, AdminStorefrontSettingsDto>
{
    public Task<AdminStorefrontSettingsDto> Handle(GetAdminStorefrontSettingsRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var settings = await db.Set<StorefrontPaymentSettings>()
                .SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
            if (settings is null)
            {
                settings = new StorefrontPaymentSettings(StorefrontPaymentSettings.SingletonId);
                db.Add(settings);
            }

            return new AdminStorefrontSettingsDto(
                settings.BankName,
                settings.AccountNumber,
                settings.AccountHolder,
                settings.Branch,
                settings.Instructions,
                settings.BankTransferEnabled,
                settings.Version);
        }, ct);
}
