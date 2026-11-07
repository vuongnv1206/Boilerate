using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Domain.Ordering;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Storefront.Settings;

public sealed record UpdateStorefrontSettingsRequest(
    string? BankName,
    string? AccountNumber,
    string? AccountHolder,
    string? Branch,
    string? Instructions,
    bool BankTransferEnabled,
    string Version) : IRequest<AdminStorefrontSettingsDto>;

public sealed class UpdateStorefrontSettingsRequestValidator : CustomValidator<UpdateStorefrontSettingsRequest>
{
    public UpdateStorefrontSettingsRequestValidator()
    {
        RuleFor(x => x.Version).MaximumLength(64);
    }
}

public sealed class UpdateStorefrontSettingsRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work) : IRequestHandler<UpdateStorefrontSettingsRequest, AdminStorefrontSettingsDto>
{
    public Task<AdminStorefrontSettingsDto> Handle(UpdateStorefrontSettingsRequest request, CancellationToken ct) =>
        work.ExecuteAsync(async () =>
        {
            var settings = await db.Set<StorefrontPaymentSettings>()
                .SingleOrDefaultAsync(s => s.Id == StorefrontPaymentSettings.SingletonId, ct);
            if (settings is null)
            {
                if (!string.IsNullOrEmpty(request.Version))
                {
                    throw new ConflictException("Settings changed; reload before editing.");
                }

                settings = new StorefrontPaymentSettings(StorefrontPaymentSettings.SingletonId);
                db.Add(settings);
            }
            else if (string.IsNullOrWhiteSpace(request.Version) || request.Version != settings.Version)
            {
                throw new ConflictException("Settings changed; reload before editing.");
            }

            settings.Update(
                request.BankName,
                request.AccountNumber,
                request.AccountHolder,
                request.Branch,
                request.Instructions,
                request.BankTransferEnabled);
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
