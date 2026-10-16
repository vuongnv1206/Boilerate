using Boilerate.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Boilerate.Infrastructure.Auth;

/// <summary>
/// Middleware để set current user từ HttpContext.User.
/// Phải đặt SAU UseAuthentication() trong pipeline.
/// </summary>
internal class CurrentUserMiddleware : IMiddleware
{
    private readonly ICurrentUserInitializer _currentUserInitializer;

    public CurrentUserMiddleware(ICurrentUserInitializer currentUserInitializer) =>
        _currentUserInitializer = currentUserInitializer;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Set current user từ HttpContext.User (đã authenticate bởi JWT middleware)
        _currentUserInitializer.SetCurrentUser(context.User);

        // Continue pipeline
        await next(context);
    }
}
