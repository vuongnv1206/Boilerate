using Boilerate.Application.Ordering.Discounts;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Ordering;

[Route("api/admin/discounts"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDiscountsController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Discount)]
    public Task<Page<DiscountDto>> List(
        CancellationToken ct,
        int page = 1,
        int size = 20,
        string? search = null,
        bool? isActive = null) =>
        Mediator.Send(new GetDiscountsRequest(page, size, search, isActive), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Discount)]
    public Task<DiscountDto> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetDiscountDetailRequest(id), ct);

    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Discount)]
    public Task<DiscountDto> Create(SaveDiscount request, CancellationToken ct) =>
        Mediator.Send(new CreateDiscountRequest(request), ct);

    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Discount)]
    public Task<DiscountDto> Update(Guid id, SaveDiscount request, CancellationToken ct) =>
        Mediator.Send(new UpdateDiscountRequest(id, request), ct);
}

