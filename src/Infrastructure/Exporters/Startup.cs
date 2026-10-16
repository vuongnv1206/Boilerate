using Boilerate.Application.Common.Exporters;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Exporters;

internal static class Startup
{
    internal static IServiceCollection AddExporters(this IServiceCollection services)
    {
        // Register Excel writer
        services.AddTransient<IExcelWriter, ClosedXMLWriter>();
        services.AddTransient<IPdfService, PdfSharpPdfService>();

        return services;
    }
}
