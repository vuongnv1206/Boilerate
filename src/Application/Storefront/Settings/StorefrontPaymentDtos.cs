namespace Boilerate.Application.Storefront.Settings;

public sealed record GuestPaymentSettingsDto(
    string? BankName,
    string? AccountNumber,
    string? AccountHolder,
    string? Branch,
    string? Instructions,
    bool BankTransferEnabled);

public sealed record AdminStorefrontSettingsDto(
    string? BankName,
    string? AccountNumber,
    string? AccountHolder,
    string? Branch,
    string? Instructions,
    bool BankTransferEnabled,
    string Version);
