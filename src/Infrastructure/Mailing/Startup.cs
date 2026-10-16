using Boilerate.Application.Common.Mailing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Mailing;

internal static class Startup
{
    internal static IServiceCollection AddMailing(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SMTPEmailSettings>(config.GetSection(nameof(SMTPEmailSettings)));
        
        // IMailService and IEmailTemplateService implement ITransientService, 
        // so they will be registered by AddServices() in Infrastructure/Startup.cs
        
        return services;
    }
}
