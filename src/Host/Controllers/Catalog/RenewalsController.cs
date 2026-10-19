using Boilerate.Application.Catalog;
using Boilerate.Application.Ordering;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Catalog;

[Authorize, Route("api/orders/{orderId:guid}/renewals"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyRenewalsController(IRenewalService renewals) : BaseApiController
{
    [HttpGet]
    public Task<List<RenewalDto>> List(Guid orderId, CancellationToken ct) => renewals.RenewalsAsync(orderId, ct);
    [HttpPut("{id:guid}/target")]
    public Task<RenewalDto> SetTarget(Guid orderId, Guid id, SetRenewalTarget request, CancellationToken ct) => renewals.SetRenewalTargetAsync(orderId, id, request, ct);
    [HttpGet("{id:guid}/target")]
    public Task<RenewalContentDto> Target(Guid orderId, Guid id, CancellationToken ct) => renewals.RevealRenewalTargetAsync(orderId, id, ct);
}

[Route("api/admin/renewals"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRenewalsController(IRenewalService renewals) : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<Page<RenewalAdminDto>> List(CancellationToken ct, int page = 1, int size = 20, RenewalStatus? status = null) => renewals.RenewalsAdminAsync(page, size, status, ct);
    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Get(Guid id, CancellationToken ct) => renewals.GetRenewalAdminAsync(id, ct);
    [HttpPost("{id:guid}/claim"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Claim(Guid id, ClaimRenewal request, CancellationToken ct) => renewals.ClaimRenewalAsync(id, request, ct);
    [HttpPost("{id:guid}/release"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Release(Guid id, ReleaseRenewal request, CancellationToken ct) => renewals.ReleaseRenewalAsync(id, request, ct);
    [HttpPost("{id:guid}/start"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Start(Guid id, StartRenewal request, CancellationToken ct) => renewals.StartRenewalAsync(id, request, ct);
    [HttpPost("{id:guid}/verify"), MustHavePermission(AppAction.Verify, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Verify(Guid id, VerifyRenewal request, CancellationToken ct) => renewals.VerifyRenewalAsync(id, request, ct);
    [HttpGet("{id:guid}/target"), MustHavePermission(AppAction.Reveal, AppFunction.Renewal)]
    public Task<RenewalContentDto> Target(Guid id, CancellationToken ct) => renewals.RevealRenewalTargetAdminAsync(id, ct);
    [HttpGet("{id:guid}/attempts/{attemptId:guid}/evidence"), MustHavePermission(AppAction.Verify, AppFunction.Renewal)]
    public Task<RenewalContentDto> Evidence(Guid id, Guid attemptId, CancellationToken ct) => renewals.RevealRenewalEvidenceAsync(id, attemptId, ct);
    [HttpGet("{id:guid}/history"), MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<List<RenewalHistoryDto>> History(Guid id, CancellationToken ct) => renewals.RenewalHistoryAsync(id, ct);
}

[Route("api/admin/orders/{orderId:guid}/renewals/provision"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRenewalProvisionController(IRenewalService renewals) : BaseApiController
{
    [HttpPost, MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<List<RenewalDto>> Provision(Guid orderId, ProvisionRenewals request, CancellationToken ct) => renewals.ProvisionRenewalsAsync(orderId, request, ct);
}
