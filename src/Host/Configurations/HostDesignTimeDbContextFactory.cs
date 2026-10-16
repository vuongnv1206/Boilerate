using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore.Design;

namespace Boilerate.Host.Configurations;

/// <summary>Startup-assembly discovery prevents EF tooling from resolving any Host adapters/options.</summary>
public sealed class HostDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) => new ApplicationDbContextFactory().CreateDbContext(args);
}
