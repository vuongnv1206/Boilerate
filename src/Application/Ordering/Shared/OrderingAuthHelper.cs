using System.Net.Mail;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Common.Exceptions;

namespace Boilerate.Application.Ordering.Shared;

public static class OrderingAuthHelper
{
    public static Guid RequireCustomer(ICurrentUser actor)
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
        {
            throw new UnauthorizedException("Sign in to use your cart and orders.");
        }

        return actor.GetUserId();
    }

    public static async Task<Guid> RequirePermissionAsync(
        ICurrentUser actor,
        IUserService users,
        string function,
        string action,
        CancellationToken ct)
    {
        Guid id = RequireCustomer(actor);
        if (!await users.HasPermissionAsync(id.ToString(), $"Permissions.{function}.{action}", ct))
        {
            throw new ForbiddenException($"{function}.{action} is required.");
        }

        return id;
    }

    public static Guid RequireGuestId(Guid id) =>
        id == Guid.Empty ? throw new DomainException("A guest session is required.") : id;

    public static string NormalizeEmail(string email)
    {
        string? trimmed = email?.Trim();
        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
        {
            throw new DomainException("A valid email address is required.");
        }

        return parsed.Address.ToUpperInvariant();
    }

    public static Task AcquireOrderingGateAsync(
        IApplicationDbContext db,
        CancellationToken ct,
        string message = "Apply ordering migrations before checkout.") =>
        CommerceConcurrency.AcquireGateAsync(db, "ordering", ct, message);
}

