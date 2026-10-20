namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>An explicitly invoked development data set. Implementations own their replay policy.</summary>
public interface ICustomSeeder
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
