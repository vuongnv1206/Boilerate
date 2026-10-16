using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerate.Infrastructure.Identity;

internal static class RequestValidation
{
    internal static async Task ValidateAsync<T>(IServiceProvider services, T request, CancellationToken ct = default)
    {
        foreach (var validator in services.GetServices<IValidator<T>>())
            await validator.ValidateAndThrowAsync(request, ct);
    }
}
