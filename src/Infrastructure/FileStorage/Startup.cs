using Microsoft.AspNetCore.Builder;
namespace Boilerate.Infrastructure.FileStorage;
internal static class Startup
{
    // Storage creates directories under its configured root when uploading, never under /app.
    internal static IApplicationBuilder UseFileStorage(this IApplicationBuilder app) => app;
}
