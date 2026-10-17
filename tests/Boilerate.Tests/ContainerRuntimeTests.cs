using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Boilerate.Application.Catalog;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Boilerate.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ContainerTheoryAttribute : TheoryAttribute
{
    public ContainerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOILERATE_CONTAINER_IMAGE")))
            Skip = "Set BOILERATE_CONTAINER_IMAGE to run the Linux container acceptance gate.";
    }
}
[Collection("Foundation")]
public sealed class ContainerRuntimeTests
{
    [ContainerTheory, Trait("Category", "ProviderIntegration"), Trait("Runtime", "Container")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task NonRootContainerPersistsMediaKeysAndSessionsAcrossRestart(string provider)
    {
        Assert.True(OperatingSystem.IsLinux(), "Container gate requires Linux host networking.");
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var oldEmail = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
        var oldPassword = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", "container@example.test");
            Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", "ContainerPassword!92837");
            using var scope = fixture.Scope();
            await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().RunAsync(["--bootstrap-admin"]);
        }
        finally { Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL", oldEmail); Environment.SetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD", oldPassword); }
        var name = "boilerate-test-" + Guid.NewGuid().ToString("N");
        var envPath = Path.GetTempFileName();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var env = fixture.Configuration.AsEnumerable().Where(p => p.Value is not null).Select(p => p.Key.Replace(":", "__") + "=" + p.Value).ToList();
        env.Add("ASPNETCORE_HTTP_PORTS=" + port);
        env.Add("ASPNETCORE_ENVIRONMENT=Production");
        await File.WriteAllLinesAsync(envPath, env);
        var volume = name + "-data";
        try
        {
            await Docker("volume", "create", volume);
            await Docker("run", "--detach", "--network", "host", "--name", name, "--env-file", envPath, "--mount", "type=volume,src=" + volume + ",dst=/data", Environment.GetEnvironmentVariable("BOILERATE_CONTAINER_IMAGE")!);
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
            await Ready(client);
            Assert.NotEqual("0", (await Docker("exec", name, "id", "-u")).Trim());
            var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("container@example.test", "ContainerPassword!92837"));
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10, 1]), "file", "image.png");
            var upload = await client.PostAsync("/api/media", form);
            upload.EnsureSuccessStatusCode();
            var media = (await upload.Content.ReadFromJsonAsync<MediaDto>())!;
            var keys = await Docker("exec", name, "sh", "-c", "ls /data/keys/key-*.xml");
            Assert.Contains("key-", keys, StringComparison.Ordinal);
            await Docker("restart", name);
            await Ready(client);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/media/" + media.Id)).StatusCode);
            Assert.Equal(keys, await Docker("exec", name, "sh", "-c", "ls /data/keys/key-*.xml"));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tokens/sessions")).StatusCode);
        }
        finally
        {
            try
            {
                await Docker("rm", "--force", name);
            }
            finally { await Docker("volume", "rm", volume); File.Delete(envPath); }
        }
    }
    private static async Task Ready(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if ((await client.GetAsync("/health/ready")).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException) { /* Kestrel is still starting; the deadline bounds retries. */ }
            catch (TaskCanceledException) { /* A single request timeout does not extend the readiness deadline. */ }
            await Task.Delay(300);
        }
        Assert.Fail("Container did not become ready.");
    }
    private static async Task<string> Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            if (!process.HasExited) process.Kill(true);
            throw;
        }
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }
}