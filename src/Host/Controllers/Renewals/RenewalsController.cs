using Boilerate.Application.Renewals;
using Boilerate.Domain.Renewals;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boilerate.Host.Controllers.Renewals;

[Authorize, Route("api/orders/{orderId:guid}/renewals"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyRenewalsController : BaseApiController
{
    [HttpGet]
    public Task<List<RenewalDto>> List(Guid orderId, CancellationToken ct) =>
        Mediator.Send(new GetMyOrderRenewalsRequest(orderId), ct);

    [HttpPut("{id:guid}/target")]
    public Task<RenewalDto> SetTarget(Guid orderId, Guid id, SetRenewalTarget request, CancellationToken ct) =>
        Mediator.Send(new SetRenewalTargetRequest(orderId, id, request), ct);

    [HttpGet("{id:guid}/target")]
    public Task<RenewalContentDto> Target(Guid orderId, Guid id, CancellationToken ct) =>
        Mediator.Send(new RevealMyRenewalTargetRequest(orderId, id), ct);
}

[Route("api/admin/renewals"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRenewalsController : BaseApiController
{
    [HttpGet, MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<Page<RenewalAdminDto>> List(
        CancellationToken ct,
        int page = 1,
        int size = 20,
        RenewalStatus? status = null,
        string? queue = null) =>
        Mediator.Send(new GetAdminRenewalsRequest(page, size, status, queue), ct);

    [HttpGet("{id:guid}"), MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Get(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetAdminRenewalDetailRequest(id), ct);

    [HttpPost("{id:guid}/claim"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Claim(Guid id, ClaimRenewal request, CancellationToken ct) =>
        Mediator.Send(new ClaimRenewalRequest(id, request), ct);

    [HttpPost("{id:guid}/release"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Release(Guid id, ReleaseRenewal request, CancellationToken ct) =>
        Mediator.Send(new ReleaseRenewalRequest(id, request), ct);

    [HttpPost("{id:guid}/start"), MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Start(Guid id, StartRenewal request, CancellationToken ct) =>
        Mediator.Send(new StartRenewalRequest(id, request), ct);

    [HttpPost("{id:guid}/verify"), MustHavePermission(AppAction.Verify, AppFunction.Renewal)]
    public Task<RenewalAdminDto> Verify(Guid id, VerifyRenewal request, CancellationToken ct) =>
        Mediator.Send(new VerifyRenewalRequest(id, request), ct);

    [HttpGet("{id:guid}/target"), MustHavePermission(AppAction.Reveal, AppFunction.Renewal)]
    public Task<RenewalContentDto> Target(Guid id, CancellationToken ct) =>
        Mediator.Send(new RevealAdminRenewalTargetRequest(id), ct);

    [HttpGet("{id:guid}/attempts/{attemptId:guid}/evidence"), MustHavePermission(AppAction.Verify, AppFunction.Renewal)]
    public Task<RenewalContentDto> Evidence(Guid id, Guid attemptId, CancellationToken ct) =>
        Mediator.Send(new RevealRenewalEvidenceRequest(id, attemptId), ct);

    [HttpGet("{id:guid}/history"), MustHavePermission(AppAction.View, AppFunction.Renewal)]
    public Task<List<RenewalHistoryDto>> History(Guid id, CancellationToken ct) =>
        Mediator.Send(new GetRenewalHistoryRequest(id), ct);
}

[Route("api/admin/orders/{orderId:guid}/renewals/provision"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminRenewalProvisionController : BaseApiController
{
    [HttpPost, MustHavePermission(AppAction.Process, AppFunction.Renewal)]
    public Task<List<RenewalDto>> Provision(Guid orderId, ProvisionRenewals request, CancellationToken ct) =>
        Mediator.Send(new ProvisionOrderRenewalsRequest(orderId, request), ct);
}

