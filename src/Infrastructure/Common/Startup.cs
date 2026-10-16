using Boilerate.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
namespace Boilerate.Infrastructure.Common;
internal static class Startup
{
    internal static IServiceCollection AddServices(this IServiceCollection services)
    {
        Register(services, typeof(ITransientService), ServiceLifetime.Transient);
        Register(services, typeof(IScopedService), ServiceLifetime.Scoped);
        Register(services, typeof(ISingletonService), ServiceLifetime.Singleton);
        return services;
    }
    private static void Register(IServiceCollection services, Type marker, ServiceLifetime lifetime)
    {
        var markers = new[] { typeof(ITransientService), typeof(IScopedService), typeof(ISingletonService) };
        var bindings = new[] { typeof(Startup).Assembly, typeof(Boilerate.Application.Startup).Assembly }
            .SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsNested && !t.IsAbstract && !t.ContainsGenericParameters && marker.IsAssignableFrom(t))
            .SelectMany(t =>
            {
                var contracts = t.GetInterfaces().Where(i => !markers.Contains(i)).ToArray();
                return (contracts.Length == 0 ? new[] { t } : contracts).Select(i => (Service: i, Implementation: t));
            });
        foreach (var group in bindings.GroupBy(x => x.Service))
        {
            if (services.Any(d => d.ServiceType == group.Key)) continue;
            var candidates = group.Select(x => x.Implementation).Distinct().ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException($"Explicit registration required for {group.Key.Name}.");
            services.Add(new ServiceDescriptor(group.Key, candidates[0], lifetime));
        }
    }
}
