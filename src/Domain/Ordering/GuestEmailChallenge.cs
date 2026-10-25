using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Exceptions;

namespace Boilerate.Domain.Ordering;

public sealed class GuestEmailChallenge : CommerceEntity
{
    public string NormalizedEmail { get; private set; } = null!;
    public string CodeHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastSentAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public int Attempts { get; private set; }

    private GuestEmailChallenge() { }

    public GuestEmailChallenge(string normalizedEmail, string codeHash, DateTime now, DateTime expiresAtUtc)
    {
        NormalizedEmail = CatalogRules.Required(normalizedEmail, 256, "Email");
        CodeHash = CatalogRules.Required(codeHash, 64, "CodeHash");
        if (now.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= now)
            throw new DomainException("OTP timestamps must be valid UTC timestamps.");
        CreatedAtUtc = now;
        LastSentAtUtc = now;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void RecordFailedAttempt()
    {
        if (ConsumedAtUtc is not null || Attempts >= 5)
            throw new DomainException("The verification code is no longer valid.");
        Attempts++;
        Touch();
    }

    public void Consume(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || now >= ExpiresAtUtc || ConsumedAtUtc is not null || Attempts >= 5)
            throw new DomainException("The verification code is no longer valid.");
        ConsumedAtUtc = now;
        Touch();
    }
}

public sealed class StorefrontPaymentSettings : CommerceEntity
{
    public static readonly Guid SingletonId = Guid.Parse("5a6bc5b2-9c2c-4e50-a392-cb4db0c2bd8e");
    public string? BankName { get; private set; }
    public string? AccountNumber { get; private set; }
    public string? AccountHolder { get; private set; }
    public string? Branch { get; private set; }
    public string? Instructions { get; private set; }
    public bool BankTransferEnabled { get; private set; }

    private StorefrontPaymentSettings() { }

    public StorefrontPaymentSettings(Guid id)
    {
        if (id != SingletonId) throw new DomainException("Only the singleton storefront settings row is allowed.");
        Id = id;
    }

    public void Update(string? bankName, string? accountNumber, string? accountHolder, string? branch,
        string? instructions, bool bankTransferEnabled)
    {
        BankName = Optional(bankName, 120, nameof(BankName));
        AccountNumber = Optional(accountNumber, 64, nameof(AccountNumber));
        AccountHolder = Optional(accountHolder, 160, nameof(AccountHolder))?.ToUpperInvariant();
        Branch = Optional(branch, 160, nameof(Branch));
        Instructions = Optional(instructions, 1000, nameof(Instructions));
        if (bankTransferEnabled && (BankName is null || AccountNumber is null || AccountHolder is null))
            throw new DomainException("Bank name, account number and account holder are required to enable bank transfer.");
        BankTransferEnabled = bankTransferEnabled;
        Touch();
    }

    private static string? Optional(string? value, int max, string field)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > max) throw new DomainException($"{field} must not exceed {max} characters.");
        return value;
    }
}
