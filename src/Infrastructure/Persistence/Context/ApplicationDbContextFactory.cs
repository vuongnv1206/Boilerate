using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Boilerate.Infrastructure.Persistence.Context;

/// <summary>EF tooling never builds the HTTP host or starts external adapters.</summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var provider = GetArgument(args, "--provider") ?? Environment.GetEnvironmentVariable("DatabaseSettings__DBProvider") ?? "mssql";
        var connection = Environment.GetEnvironmentVariable("DatabaseSettings__ConnectionString");
        // Only offline scaffolding/script generation may opt into a deliberately unreachable connection.
        if (string.IsNullOrWhiteSpace(connection))
        {
            if (!args.Contains("--design-only"))
                throw new InvalidOperationException("Set DatabaseSettings__ConnectionString explicitly. Use --design-only only for offline migration scaffolding or scripts.");
            connection = provider == "postgresql"
                ? "Host=127.0.0.1;Port=1;Database=boilerate_design;Username=design;Timeout=1"
                : "Server=tcp:127.0.0.1,1;Database=boilerate_design;Integrated Security=true;Connect Timeout=1;TrustServerCertificate=true";
        }
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        builder.UseDatabase(provider, connection);
        return new ApplicationDbContext(builder.Options, new CurrentUser(), new NewtonSoftService(), new DesignTimeEvents());
    }
    private static string? GetArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    private sealed class DesignTimeEvents : IEventPublisher
    {
        public Task PublishAsync(Boilerate.Domain.Common.Contracts.IEvent @event) => Task.CompletedTask;
    }
}
