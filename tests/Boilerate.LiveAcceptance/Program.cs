using System.Security.Cryptography;
using System.Text.Json;
using Boilerate.Domain.Identity;
using Boilerate.Application.Common.Mailing;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Host.Configurations;
using Boilerate.Host.Controllers.Identity;
using Boilerate.Infrastructure;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Boilerate.LiveAcceptance;
using Boilerate.Tests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Test-only entry point. It never accepts a target database name or uses Host user secrets.
// CLI: dotnet run --project tests/Boilerate.LiveAcceptance -- --ready-file <absolute path>
//      --stop-file <absolute path>. Touch stop-file for graceful shutdown and DB cleanup.
// BOILERATE_TEST_MSSQL selects the SQL Server instance; Windows defaults to LocalDB.
var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
for (var index = 0; index < args.Length; index += 2)
{
    if (index + 1 >= args.Length || args[index] is not ("--ready-file" or "--stop-file"))
        throw new ArgumentException("Expected --ready-file <absolute path> --stop-file <absolute path>.");
    arguments.Add(args[index], args[index + 1]);
}
var readyFile = RequiredPath("--ready-file");
var stopFile = RequiredPath("--stop-file");
if (File.Exists(readyFile) || File.Exists(stopFile))
    throw new InvalidOperationException("Use fresh readiness and shutdown paths for every run.");
Directory.CreateDirectory(Path.GetDirectoryName(readyFile)!);
var runtimeRoot = Path.Combine(Path.GetDirectoryName(readyFile)!, "runtime");
Directory.CreateDirectory(runtimeRoot);
var adminEmail = "live-admin@example.test";
var password = "Live!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
var configuration = new Dictionary<string, string?>
{
    ["DevelopmentSeed:ActorEmail"] = adminEmail,
    ["Storage:Root"] = Path.Combine(runtimeRoot, "Files"),
    ["DataProtection:KeysPath"] = Path.Combine(runtimeRoot, "keys"),
    ["DataProtection:ApplicationName"] = "Boilerate.LiveAcceptance",
    ["Commerce:WorkerEnabled"] = "false",
    ["SecuritySettings:RequireConfirmedAccount"] = "false",
    ["SecuritySettings:PasswordRecoveryEnabled"] = "false",
    ["SecuritySettings:AllowSelfRegistration"] = "false",
};

await using var database = new FoundationDatabase("mssql", catalog: true, configOverrides: configuration);
database.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Development;
await database.CreateAsync();
var originalEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
var originalPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
try
{
    Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", adminEmail);
    Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", password);
    using var scope = database.Scope();
    await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
}
finally
{
    Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", originalEmail);
    Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", originalPassword);
}

using (var scope = database.Scope())
{
    await scope.ServiceProvider.GetRequiredService<CustomSeederRunner>().RunAsync();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var basic = new ApplicationUser
    {
        UserName = "live-basic@example.test", Email = "live-basic@example.test",
        EmailConfirmed = true, IsActive = true, FirstName = "Live basic",
    };
    var result = await users.CreateAsync(basic, password);
    if (!result.Succeeded)
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    result = await users.AddToRoleAsync(basic, "Basic");
    if (!result.Succeeded)
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
}

Guid adminActorId;
using (var scope = database.Scope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    adminActorId = Guid.Parse(await db.Users.Where(user => user.Email == adminEmail).Select(user => user.Id).SingleAsync());
}
var acceptance = await AdminAcceptanceData.SeedAsync(database, adminActorId, password);
var storefront = await GuestStorefrontAcceptanceData.SeedAsync(database, adminActorId);

// Same application services/controllers/middleware as TestHostApplication, on a real socket.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    EnvironmentName = Environments.Development,
    ApplicationName = typeof(TokensController).Assembly.GetName().Name,
    ContentRootPath = runtimeRoot,
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddConfiguration(database.Configuration);
database.ConfigureServices(builder.Services);
builder.Services.AddSingleton<AcceptanceMailSink>();
builder.Services.Replace(ServiceDescriptor.Transient<IMailService>(services => services.GetRequiredService<AcceptanceMailSink>()));
builder.Services.Replace(ServiceDescriptor.Singleton<IHostEnvironment>(builder.Environment));
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("authentication", limiter =>
{
    limiter.PermitLimit = 500;
    limiter.Window = TimeSpan.FromMinutes(1);
}));
builder.Services.AddControllers().AddApplicationPart(typeof(TokensController).Assembly)
    .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogControllerFeatureProvider(true, false)));
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app = builder.Build();
app.UseInfrastructure(database.Configuration);
app.MapEndpoints();
app.MapPost("/__acceptance/outbox/drain", async (IServiceScopeFactory scopes, CancellationToken ct) =>
{
    var processed = 0;
    while (processed < 200)
    {
        using var scope = scopes.CreateScope();
        if (!await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOneAsync(ct)) break;
        processed++;
    }
    return Results.Ok(new { processed });
});
app.MapGet("/__acceptance/mail-sink/messages", (AcceptanceMailSink sink) => Results.Ok(sink.Snapshot()));
await app.StartAsync();

try
{
    using var scope = database.Scope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manifest = new
    {
        schemaVersion = 1,
        runId = database.Name,
        database = database.Name,
        migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray(),
        backendUrl = app.Urls.Single(),
        credentials = new
        {
            admin = new { email = adminEmail, password },
            basic = new { email = "live-basic@example.test", password },
        },
        actors = acceptance.Actors,
        scenarios = acceptance.Scenarios,
        storefront,
        products = await db.Products.AsNoTracking().OrderBy(product => product.Slug)
            .Select(product => new { product.Id, product.Slug, product.Name, product.Version, product.ProductTypeId }).ToListAsync(),
        categories = await db.Categories.AsNoTracking().OrderBy(category => category.Slug)
            .Select(category => new { category.Id, category.Slug, category.Name, category.Version }).ToListAsync(),
        productTypes = await db.ProductTypes.AsNoTracking().OrderBy(type => type.Code)
            .Select(type => new { type.Id, type.Code, type.Name, type.Version }).ToListAsync(),
        orders = await db.Orders.AsNoTracking().OrderBy(order => order.Number)
            .Select(order => new { order.Id, order.Number, order.Version, order.Status, order.PaymentStatus, order.FulfillmentStatus }).ToListAsync(),
    };
    var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    await File.WriteAllTextAsync(readyFile + ".tmp", json);
    File.Move(readyFile + ".tmp", readyFile);
    Console.WriteLine($"LIVE_ACCEPTANCE_READY {manifest.backendUrl} database={database.Name}");
    var stopped = app.WaitForShutdownAsync();
    while (!File.Exists(stopFile) && !stopped.IsCompleted)
        await Task.WhenAny(stopped, Task.Delay(250));
    await app.StopAsync();
    await stopped;
}
finally
{
    // The manifest contains one-run credentials; reports must never retain it.
    File.Delete(readyFile);
    File.Delete(readyFile + ".tmp");
}

string RequiredPath(string key)
{
    if (!arguments.TryGetValue(key, out var value) || !Path.IsPathFullyQualified(value))
        throw new ArgumentException($"{key} must be an absolute path.");
    return Path.GetFullPath(value);
}
