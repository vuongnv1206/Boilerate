namespace Boilerate.Domain.Identity;

/// <summary>Portable database row used to serialize changes to the administration path.</summary>
public sealed class AuthorizationLock
{
    public int Id { get; set; } = 1;
    public string Version { get; set; } = Guid.NewGuid().ToString("N");
}
