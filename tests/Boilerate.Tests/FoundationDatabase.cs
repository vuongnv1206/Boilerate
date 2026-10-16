using System.Security.Claims;
using Boilerate.Application;
using Boilerate.Infrastructure;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Boilerate.Tests;

internal sealed class FoundationDatabase : IAsyncDisposable
{
    public IConfiguration Configuration { get; }
    public ServiceProvider Services { get; }
    public string Name { get; } = "boilerate_test_" + Guid.NewGuid().ToString("N");
    private readonly string _provider;
    private readonly string _adminConnection;
    private bool _created;

    public FoundationDatabase(string provider, bool catalog = false, bool notifications = false)
    {
        _provider = provider;
        string connection;
        if (provider == "mssql")
        {
            var configured = Environment.GetEnvironmentVariable("BOILERATE_TEST_MSSQL");
            if (configured is null && !OperatingSystem.IsWindows()) throw new InvalidOperationException("Set BOILERATE_TEST_MSSQL for provider integration tests.");
            var sql = new SqlConnectionStringBuilder(configured ??
                @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true");
            sql.InitialCatalog = "master"; _adminConnection = sql.ConnectionString;
            sql.InitialCatalog = Name; connection = sql.ConnectionString;
        }
        else
        {
            var configured = Environment.GetEnvironmentVariable("BOILERATE_TEST_POSTGRES") ??
                throw new InvalidOperationException("Set BOILERATE_TEST_POSTGRES for provider integration tests.");
            var pg = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
            _adminConnection = pg.ConnectionString; pg.Database = Name; connection = pg.ConnectionString;
        }
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseSettings:DBProvider"] = provider, ["DatabaseSettings:ConnectionString"] = connection,
            ["SecuritySettings:JwtSettings:Key"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["SecuritySettings:JwtSettings:Issuer"] = "Boilerate",
            ["SecuritySettings:JwtSettings:Audience"] = "Boilerate.Api",
            ["SecuritySettings:JwtSettings:TokenExpirationInMinutes"] = "15",
            ["SecuritySettings:JwtSettings:RefreshTokenExpirationInDays"] = "30",
            ["CacheSettings:UseDistributedCache"] = "false", ["HangfireStorageSettings:Enabled"] = "false",
            ["BlobStorageSettings:Enabled"] = "false", ["BlobStorageSettings:Provider"] = "Azure",
            ["Modules:Catalog:Enabled"] = catalog.ToString(), ["Modules:Notifications:Enabled"] = notifications.ToString()
        }).Build();
        var services = new ServiceCollection(); ConfigureServices(services);
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(Configuration);
        services.TryAddSingleton<IHostApplicationLifetime, LifetimeStub>();
        services.AddSingleton<IHostEnvironment>(new EnvironmentStub());
        services.AddLogging(); services.AddHttpClient();
        services.AddApplication(); services.AddInfrastructure(Configuration);
    }

    public async Task CreateAsync(bool migrate = true)
    {
        if (!Name.StartsWith("boilerate_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test database name.");
        if (_provider == "mssql")
        {
            await using var connection = new SqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{Name}]"; await command.ExecuteNonQueryAsync();
        }
        else
        {
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{Name}\""; await command.ExecuteNonQueryAsync();
        }
        _created = true;
        if (migrate)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        }
    }

    public IServiceScope Scope(Guid? actor = null)
    {
        var scope = Services.CreateScope();
        if (actor.HasValue)
            scope.ServiceProvider.GetRequiredService<ICurrentUserInitializer>().SetCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, actor.Value.ToString()) }, "Test")));
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        if (!_created) return;
        if (!Name.StartsWith("boilerate_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe cleanup target.");
        if (_provider == "mssql")
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(_adminConnection); await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"IF DB_ID('{Name}') IS NOT NULL BEGIN ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}]; END";
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(_adminConnection); await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)"; await command.ExecuteNonQueryAsync();
        }
    }
    private sealed class LifetimeStub : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Boilerate.Host";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
