using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class TemplateRuntimeSmokeTests
{
    [Theory, Trait("Category", "ProviderIntegration"), Trait("Smoke", "Template")]
    [InlineData("mssql", false), InlineData("mssql", true)]
    [InlineData("postgresql", false), InlineData("postgresql", true)]
    public async Task ActualCliMigratesBootstrapsAndRestartsWithoutChangingAcl(string provider, bool catalog)
    {
        await using var fixture = new FoundationDatabase(provider, catalog);
        await fixture.CreateAsync(migrate: false);
        var root = FindRoot();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var environment = fixture.Configuration.AsEnumerable().Where(x => x.Value is not null)
            .ToDictionary(x => x.Key.Replace(":", "__"), x => x.Value!, StringComparer.OrdinalIgnoreCase);
        environment["BOILERATE_BOOTSTRAP_EMAIL"] = "cli@example.test";
        environment["BOILERATE_BOOTSTRAP_PASSWORD"] = "UniqueCliPassword!982345";
        environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        environment["DOTNET_ENVIRONMENT"] = "Development";
        environment["Modules__Notifications__Enabled"] = "false";
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        var migrationProject = provider == "mssql" ? "src/Migrators.MSSQL" : "src/Migrators.PostgreSQL";
        await Command(root, environment, "ef", "database", "update", "--project", migrationProject,
            "--startup-project", "src/Host", "--configuration", configuration, "--no-build", "--", "--provider", provider);
        var hostArguments = new[] { "run", "--project", "src/Host", "--configuration", configuration, "--no-build", "--no-launch-profile" };
        await Command(root, environment, hostArguments.Concat(new[] { "--", "--bootstrap-admin" }).ToArray());
        List<string> grants;
        Guid retainedProductId;
        using (var scope = fixture.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = new Category("Retained category", "Survives module toggle/restart");
            db.Categories.Add(category); // EF assigns the Guid before it becomes the product foreign key.
            var product = new Product("Retained product", null, 123.45m, category.Id, null);
            db.Products.Add(product);
            var role = new ApplicationRole("RestartProbe"); db.Roles.Add(role);
            var pair = await db.ActionInFunctions.FirstAsync();
            var probe = new Permission(role.Id, pair.FunctionId, pair.ActionId); db.Permissions.Add(probe);
            await db.SaveChangesAsync();
            db.Permissions.Remove(probe); await db.SaveChangesAsync();
            retainedProductId = product.Id;
            grants = await GrantSnapshot(db);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        for (var restart = 0; restart < 3; restart++)
        {
            // Flip the route flag against the same database, then return to the original state.
            var catalogForRun = restart == 1 ? !catalog : catalog;
            environment["Modules__Catalog__Enabled"] = catalogForRun.ToString();
            using var process = Start(root, environment, hostArguments);
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(environment["ASPNETCORE_URLS"]), Timeout = TimeSpan.FromSeconds(15) };
                var ready = false;
                var readinessDeadline = DateTime.UtcNow.AddSeconds(45);
                while (DateTime.UtcNow < readinessDeadline)
                {
                    if (process.HasExited) throw new InvalidOperationException("Host exited before readiness.");
                    try { ready = (await client.GetAsync("/health/ready")).IsSuccessStatusCode; } catch (HttpRequestException)
                    {
                        // Retry until the deadline while Kestrel/EF starts; process output is captured separately.
                    }
                    catch (TaskCanceledException)
                    {
                        // A cold readiness query may exceed one HTTP timeout; the overall deadline still applies.
                    }
                    if (ready) break;
                    await Task.Delay(250);
                }
                Assert.True(ready, "Host did not become ready.");
                var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
                Assert.Equal(catalogForRun, swagger.Contains("/api/products", StringComparison.Ordinal));
                var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("cli@example.test", environment["BOILERATE_BOOTSTRAP_PASSWORD"]));
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
                client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/actions")).StatusCode);
                using var scope = fixture.Scope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(grants, await GrantSnapshot(db));
                var retained = await db.Products.SingleAsync(p => p.Id == retainedProductId);
                Assert.Equal(123.45m, retained.Price);
                Assert.Equal("Retained product", retained.Name);
                Assert.False(db.Database.HasPendingModelChanges());
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static async Task<List<string>> GrantSnapshot(ApplicationDbContext db)
    {
        var rows = await db.Permissions.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        return rows.Select(p => $"{p.Id}|{p.RoleId}|{p.FunctionId}|{p.ActionId}|{p.DeletedOn:O}|{p.DeletedBy}")
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static string FindRoot()
    {
        var configured = Environment.GetEnvironmentVariable("BOILERATE_TEMPLATE_ROOT");
        if (configured is not null) return Path.GetFullPath(configured);
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "src/Host/Host.csproj"))) return directory.FullName;
        throw new InvalidOperationException("Could not find solution root.");
    }

    private static Process Start(string root, Dictionary<string, string> environment, params string[] arguments)
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var item in environment) info.Environment[item.Key] = item.Value;
        var process = new Process { StartInfo = info };
        var logDirectory = Path.Combine(root, "artifacts", "verification");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "cli-" + Guid.NewGuid().ToString("N") + ".log");
        var logLock = new object();
        void Capture(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is null) return;
            var line = e.Data;
            foreach (var entry in environment.Where(x => x.Key.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
                         x.Key.Contains("Password", StringComparison.OrdinalIgnoreCase) || x.Key.Contains("Connection", StringComparison.OrdinalIgnoreCase)))
                if (entry.Value.Length != 0) line = line.Replace(entry.Value, "[redacted]", StringComparison.Ordinal);
            lock (logLock) File.AppendAllText(logPath, line + Environment.NewLine);
        }
        process.OutputDataReceived += Capture; process.ErrorDataReceived += Capture;
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static async Task Command(string root, Dictionary<string, string> environment, params string[] arguments)
    {
        using var process = Start(root, environment, arguments);
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        Assert.Equal(0, process.ExitCode);
    }
}
