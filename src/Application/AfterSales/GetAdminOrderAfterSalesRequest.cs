using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Persistence;
using Boilerate.Application.Common.Validation;
using Boilerate.Application.Identity.Users;
using Boilerate.Application.Ordering.Shared;
using FluentValidation;
using MediatR;

namespace Boilerate.Application.AfterSales;

public sealed record GetAdminOrderAfterSalesRequest(Guid OrderId) : IRequest<AfterSalesDto>;

public sealed class GetAdminOrderAfterSalesRequestValidator : CustomValidator<GetAdminOrderAfterSalesRequest>
{
    public GetAdminOrderAfterSalesRequestValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public sealed class GetAdminOrderAfterSalesRequestHandler(
    IApplicationDbContext db,
    ICommerceUnitOfWork work,
    ICurrentUser actor,
    IUserService users) : IRequestHandler<GetAdminOrderAfterSalesRequest, AfterSalesDto>
{
    public async Task<AfterSalesDto> Handle(GetAdminOrderAfterSalesRequest request, CancellationToken ct)
    {
        await OrderingAuthHelper.RequirePermissionAsync(actor, users, "AfterSales", "View", ct);
        return await AfterSalesHelper.ReadSummaryAsync(db, work, request.OrderId, null, ct);
    }
}

