using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Boilerate.Application.Catalog;
using Boilerate.Application.Identity.Tokens;
using Microsoft.Extensions.Configuration;
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
    public async Task ProductionImageOperatorAndReplacementPreserveMediaKeysAndSessions(string provider)
    {
        Assert.True(OperatingSystem.IsLinux(), "Container gate requires Linux host networking.");
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        var name = "boilerate-test-" + Guid.NewGuid().ToString("N");
        var operatorName = name + "-operator";
        var envPath = Path.Combine(Path.GetTempPath(), name + ".env");
        var bootstrapEnvPath = Path.Combine(Path.GetTempPath(), operatorName + ".env");
        var image = Environment.GetEnvironmentVariable("BOILERATE_CONTAINER_IMAGE")!;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var env = fixture.Configuration.AsEnumerable().Where(p => p.Value is not null).Select(p => p.Key.Replace(":", "__") + "=" + p.Value).ToList();
        env.Add("ASPNETCORE_HTTP_PORTS=" + port);
        env.Add("ASPNETCORE_ENVIRONMENT=Production");
        env.Add("DOTNET_ENVIRONMENT=Production");
        var volume = name + "-data";
        Exception? failure = null;
        try
        {
            await WriteEnvironment(envPath, env);
            await WriteEnvironment(bootstrapEnvPath, env.Concat([
                "BOILERATE_BOOTSTRAP_EMAIL=container@example.test",
                "BOILERATE_BOOTSTRAP_PASSWORD=ContainerPassword!92837"]));
            await Docker("volume", "create", volume);
            // Migration is a preceding deployment step; the production image performs only explicit operator commands.
            await Docker("run", "--rm", "--network", "host", "--name", operatorName, "--env-file", bootstrapEnvPath,
                "--mount", "type=volume,src=" + volume + ",dst=/data", image, "--bootstrap-admin");
            File.Delete(bootstrapEnvPath);
            await Docker("run", "--rm", "--network", "host", "--name", operatorName, "--env-file", envPath,
                "--mount", "type=volume,src=" + volume + ",dst=/data", image, "--preflight");
            var originalContainer = await StartContainer();
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(5) };
            await Ready(client);
            Assert.NotEqual("0", (await Docker("exec", name, "id", "-u")).Trim());
            var login = await client.PostAsJsonAsync("/api/tokens/get", new TokenRequest("container@example.test", "ContainerPassword!92837"));
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", token.Token);
            using var form = new MultipartFormDataContent();
            byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10, 1];
            form.Add(new ByteArrayContent(bytes), "file", "image.png");
            var upload = await client.PostAsync("/api/media", form);
            upload.EnsureSuccessStatusCode();
            var media = (await upload.Content.ReadFromJsonAsync<MediaDto>())!;
            var keys = await Docker("exec", name, "sh", "-c", "sha256sum /data/keys/key-*.xml");
            Assert.Contains("key-", keys, StringComparison.Ordinal);
            await Docker("rm", "--force", name);
            var replacementContainer = await StartContainer();
            Assert.NotEqual(originalContainer.Trim(), replacementContainer.Trim());
            await Ready(client);
            using var restoredMedia = await client.GetAsync("/api/media/" + media.Id);
            Assert.Equal(HttpStatusCode.OK, restoredMedia.StatusCode);
            Assert.Equal(bytes, await restoredMedia.Content.ReadAsByteArrayAsync());
            Assert.Equal(keys, await Docker("exec", name, "sh", "-c", "sha256sum /data/keys/key-*.xml"));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tokens/sessions")).StatusCode);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            List<Exception> cleanupFailures = [];
            foreach (var resource in new[] { name, operatorName })
            {
                try { await DockerAllowMissing("rm", "--force", resource); }
                catch (Exception ex) { cleanupFailures.Add(ex); }
            }
            try
            {
                await DockerAllowMissing("volume", "rm", volume);
            }
            catch (Exception ex) { cleanupFailures.Add(ex); }
            finally
            {
                foreach (var path in new[] { envPath, bootstrapEnvPath })
                {
                    try { File.Delete(path); }
                    catch (Exception ex) { cleanupFailures.Add(ex); }
                }
            }
            if (failure is null && cleanupFailures.Count > 0)
                throw new AggregateException("Container acceptance cleanup failed.", cleanupFailures);
        }

        Task<string> StartContainer() => Docker("run", "--detach", "--network", "host", "--name", name,
            "--env-file", envPath, "--mount", "type=volume,src=" + volume + ",dst=/data", image);
    }

    private static async Task WriteEnvironment(string path, IEnumerable<string> values)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream);
        foreach (var value in values)
            await writer.WriteLineAsync(value);
    }
    private static async Task Ready(HttpClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var ready = await client.GetAsync("/health/ready");
                using var live = await client.GetAsync("/health/live");
                if (ready.IsSuccessStatusCode && live.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException) { /* Kestrel is still starting; the deadline bounds retries. */ }
            catch (TaskCanceledException) { /* A single request timeout does not extend the readiness deadline. */ }
            await Task.Delay(300);
        }
        Assert.Fail("Container did not become ready.");
    }
    private static Task<string> Docker(params string[] arguments) => DockerCore(false, arguments);
    private static Task<string> DockerAllowMissing(params string[] arguments) => DockerCore(true, arguments);
    private static async Task<string> DockerCore(bool allowMissing, params string[] arguments)
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
        var errorText = await error;
        var missing = allowMissing && (errorText.Contains("No such container", StringComparison.OrdinalIgnoreCase) ||
            errorText.Contains("no such volume", StringComparison.OrdinalIgnoreCase));
        // Operator failures may include provider details. Never put stdout, stderr or environment values in test logs.
        Assert.True(process.ExitCode == 0 || missing, $"Docker '{arguments[0]}' failed with exit code {process.ExitCode}.");
        return await output;
    }
}
