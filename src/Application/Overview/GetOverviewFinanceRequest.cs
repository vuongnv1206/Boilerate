using System.Globalization;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Common.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Application.Overview;

public sealed record GetOverviewFinanceRequest(DateOnly? From = null, DateOnly? To = null) : IRequest<OverviewFinanceDto>;

public sealed class GetOverviewFinanceRequestValidator : CustomValidator<GetOverviewFinanceRequest>
{
    public GetOverviewFinanceRequestValidator()
    {
        RuleFor(x => x)
            .Cascade(CascadeMode.Stop)
            .Must(x => x.From.HasValue == x.To.HasValue)
            .WithMessage("Supply both from and to dates.")
            .Must(x => !x.From.HasValue || !x.To.HasValue || (x.From.Value != DateOnly.MinValue && x.From.Value <= x.To.Value && x.To.Value.DayNumber - x.From.Value.DayNumber < 90))
            .WithMessage("Choose an ordered date range of at most 90 days ending no later than today.");
    }
}

public sealed class GetOverviewFinanceRequestHandler(
    IApplicationDbContext db,
    ICurrentUser actor,
    IUserService users,
    TimeProvider clock) : IRequestHandler<GetOverviewFinanceRequest, OverviewFinanceDto>
{
    public async Task<OverviewFinanceDto> Handle(GetOverviewFinanceRequest request, CancellationToken ct)
    {
        if (!actor.IsAuthenticated() || actor.GetUserId() == Guid.Empty)
        {
            throw new UnauthorizedException("Sign in to view the overview.");
        }

        if (!await users.HasPermissionAsync(actor.GetUserId().ToString(), "Permissions.FinancialSummary.View", ct))
        {
            throw new ForbiddenException("FinancialSummary.View is required.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var start = request.From ?? today.AddDays(-6);
        var end = request.To ?? today;
        if (start == DateOnly.MinValue || start > end || end > today || end.DayNumber - start.DayNumber >= 90)
        {
            throw new DomainException("Choose an ordered date range of at most 90 days ending no later than today.");
        }

        // Vietnam has a stable UTC+07 offset. Convert the inclusive business dates to a half-open UTC range.
        var fromUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var receipts = await db.PaymentReceipts.AsNoTracking()
            .Where(r => r.ReceivedAtUtc >= fromUtc && r.ReceivedAtUtc < toUtc)
            .GroupBy(r => r.ReceivedAtUtc.AddHours(7).Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
            .ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.RefundedAtUtc >= fromUtc && r.RefundedAtUtc < toUtc)
            .GroupBy(r => r.RefundedAtUtc.AddHours(7).Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
            .ToListAsync(ct);
        var receivedByDate = receipts.ToDictionary(r => DateOnly.FromDateTime(r.Date), r => r.Amount);
        var refundedByDate = refunds.ToDictionary(r => DateOnly.FromDateTime(r.Date), r => r.Amount);
        var days = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(offset =>
        {
            var date = start.AddDays(offset);
            decimal received = receivedByDate.GetValueOrDefault(date);
            decimal refunded = refundedByDate.GetValueOrDefault(date);
            return new OverviewFinanceDay(date, Money(received), Money(refunded), Money(received - refunded));
        }).ToList();
        decimal receivedTotal = receipts.Sum(d => d.Amount);
        decimal refundedTotal = refunds.Sum(d => d.Amount);
        return new(now, start, end, "Asia/Ho_Chi_Minh", "VND", Money(receivedTotal), Money(refundedTotal),
            Money(receivedTotal - refundedTotal), days);
    }

    private static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
}
