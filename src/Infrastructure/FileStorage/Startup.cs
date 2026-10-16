using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Boilerate.Infrastructure.FileStorage;

internal static class Startup
{
    internal static IApplicationBuilder UseFileStorage(this IApplicationBuilder app)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Files");

        if (!Directory.Exists(filePath))
        {
            Directory.CreateDirectory(filePath);
        }

        return app;
    }
}
