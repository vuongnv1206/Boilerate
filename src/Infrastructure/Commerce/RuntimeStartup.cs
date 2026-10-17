using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace Boilerate.Infrastructure.Commerce;

public static class RuntimeStartup
{
    public static IServiceCollection AddDeploymentRuntime(this IServiceCollection services, IConfiguration config)
    {
        var keys = config["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keys))
        {
            Directory.CreateDirectory(Path.GetFullPath(keys));
            services.AddDataProtection().SetApplicationName(config["DataProtection:ApplicationName"] ?? "Boilerate")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keys)));
        }
        if (config.GetValue<bool>("ReverseProxy:Enabled"))
        {
            var addresses = config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            if (addresses.Length == 0)
                throw new InvalidOperationException("ReverseProxy requires explicit trusted proxy IP addresses.");
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var address in addresses)
                    options.KnownProxies.Add(IPAddress.Parse(address));
            });
        }
        var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy("storefront", policy =>
        {
            if (origins.Length > 0)
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));
        return services;
    }
}