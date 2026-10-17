using Boilerate.Infrastructure.Commerce;
using Microsoft.AspNetCore.DataProtection;
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
}
