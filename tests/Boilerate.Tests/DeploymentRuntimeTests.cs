using System.Net;
using Boilerate.Infrastructure.Commerce;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Boilerate.Tests;
public sealed class DeploymentRuntimeTests
{
    [Fact]
    public void DataProtectionPurposeSurvivesIndependentServiceProviders()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(),"boilerate-keys-"+Guid.NewGuid().ToString("N")));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DataProtection:KeysPath"] = root }).Build();
        try
        {
            string token;
            using (var first = new ServiceCollection().AddLogging().AddDeploymentRuntime(config).BuildServiceProvider())
                token = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("Identity-token-test").Protect("confirmation-token");
            using var restarted = new ServiceCollection().AddLogging().AddDeploymentRuntime(config).BuildServiceProvider();
            Assert.Equal("confirmation-token",restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("Identity-token-test").Unprotect(token));
        }
        finally
        {
            // Only delete our freshly generated flat key directory, without recursive traversal.
            foreach (var file in Directory.EnumerateFiles(root,"key-*.xml",SearchOption.TopDirectoryOnly)) File.Delete(file);
            Directory.Delete(root);
        }
    }
    [Fact]
    public void ReverseProxyCannotBeEnabledWithoutTrustedAddresses()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ReverseProxy:Enabled"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddDeploymentRuntime(config));
    }

    [Theory]
    [InlineData("10.0.0.10", "203.0.113.11", "https", "203.0.113.11", "https", "")]
    [InlineData("10.0.0.99", "203.0.113.11", "https", "10.0.0.99", "http", "203.0.113.11")]
    [InlineData("10.0.0.10", "203.0.113.11, 198.51.100.21", "http, https", "198.51.100.21", "https", "203.0.113.11")]
    public async Task ForwardedHeadersTrustOnlyConfiguredPeerAndOneHop(string peer, string forwardedFor,
        string forwardedProto, string expectedAddress, string expectedScheme, string expectedRemainingFor)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Enabled"] = "true",
            ["ReverseProxy:KnownProxies:0"] = "10.0.0.10"
        }).Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDeploymentRuntime(config);
        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.Run(context =>
        {
            context.Items["forwarded-snapshot"] = new ForwardingSnapshot(
                context.Connection.RemoteIpAddress?.ToString(), context.Request.Scheme,
                context.Request.Headers["X-Forwarded-For"].ToString());
            return Task.CompletedTask;
        });
        await app.StartAsync();
        var result = await app.GetTestServer().SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Scheme = "http";
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        });
        var observed = Assert.IsType<ForwardingSnapshot>(result.Items["forwarded-snapshot"]);
        Assert.Equal(expectedAddress, observed.Address);
        Assert.Equal(expectedScheme, observed.Scheme);
        Assert.Equal(expectedRemainingFor, observed.RemainingFor);
    }

    private sealed record ForwardingSnapshot(string? Address, string Scheme, string RemainingFor);
}
