using Boilerate.Application.Catalog;
using Boilerate.Application.Ordering;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Route("api/admin/discounts"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDiscountsController(IDiscountService discounts) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Discount)]
    public Task<Page<DiscountDto>> List(CancellationToken ct, int page = 1, int size = 20, string? search = null, bool? isActive = null)
        => discounts.ListAsync(page, size, search, isActive, ct);
    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Discount)]
    public Task<DiscountDto> Get(Guid id, CancellationToken ct) => discounts.GetAsync(id, ct);
    [HttpPost, MustHavePermission(AppAction.Create, AppFunction.Discount)]
    public Task<DiscountDto> Create(SaveDiscount request, CancellationToken ct) => discounts.CreateAsync(request, ct);
    [HttpPut("{id:guid}"), MustHavePermission(AppAction.Update, AppFunction.Discount)]
    public Task<DiscountDto> Update(Guid id, SaveDiscount request, CancellationToken ct) => discounts.UpdateAsync(id, request, ct);
}
