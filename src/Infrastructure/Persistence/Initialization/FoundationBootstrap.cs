using System.Text.Json;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Identity;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Boilerate.Infrastructure.Persistence.Initialization;

/// <summary>Operator commands only. The HTTP startup path never calls this service.</summary>
public sealed class FoundationBootstrap : IScopedService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IConfiguration _config;
    public FoundationBootstrap(ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles, IConfiguration config) => (_db, _users, _roles, _config) = (db, users, roles, config);

    private IEnumerable<(string Function, string[] Actions)> Manifest()
    {
        yield return ("Action", new[] { "View", "Create", "Update", "Delete" });
        yield return ("Function", new[] { "View", "Create", "Update", "Delete" });
        yield return ("Role", new[] { "View", "Create", "Update", "Delete" });
        yield return ("User", new[] { "View", "Create", "Update", "Export" });
        yield return ("Users", new[] { "View", "Export" });
        yield return ("Hangfire", new[] { "View" });
        if (_config.GetValue<bool>("Modules:Catalog:Enabled"))
        {
            yield return ("Product", new[] { "View", "Create", "Update", "Publish" });
            yield return ("ProductType", new[] { "View", "Create", "Update" });
            yield return ("Price", new[] { "Update" });
            yield return ("Inventory", new[] { "View", "Adjust", "Initialize" });
            yield return ("DigitalInventory", new[] { "View", "Import", "Update", "Reveal" });
            yield return ("Order", new[] { "View", "Cancel", "Fulfill" });
            yield return ("Payment", new[] { "Confirm" });
            yield return ("Shipping", new[] { "View", "Dispatch", "Deliver" });
            yield return ("Renewal", new[] { "View", "Process", "Verify", "Reveal" });
            yield return ("Outbox", new[] { "View", "Replay" });
            yield return ("Category", new[] { "View", "Create", "Update", "Delete" });
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    public async Task RunAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Contains("--preflight")) { await PreflightAsync(ct); return; }
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (await _db.AuthorizationLocks.Where(x => x.Id == 1).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.Version, Guid.NewGuid().ToString("N")), ct) != 1)
            throw new ConflictException("Apply foundation migrations before bootstrap.");
        if (args.Contains("--bootstrap-admin"))
        {
            if (await _db.Users.AnyAsync(ct) || await _db.Roles.AnyAsync(ct) ||
                await _db.Actions.AnyAsync(ct) || await _db.Functions.AnyAsync(ct) ||
                await _db.Permissions.IgnoreQueryFilters().AnyAsync(ct))
                throw new ConflictException("Bootstrap requires an empty Identity installation; existing ACL data is never overwritten.");
            var email = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_EMAIL");
            var password = Environment.GetEnvironmentVariable("BOILERATE_BOOTSTRAP_PASSWORD");
            if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _) ||
                string.IsNullOrWhiteSpace(password) || password.Length < 12)
                throw new InvalidOperationException("Supply BOILERATE_BOOTSTRAP_EMAIL and a unique BOILERATE_BOOTSTRAP_PASSWORD of at least 12 characters.");
            Check(await _roles.CreateAsync(new ApplicationRole("Admin", "Central security administrators")));
            Check(await _roles.CreateAsync(new ApplicationRole("Basic", "No grants until explicitly assigned")));
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, IsActive = true, FirstName = "Administrator" };
            Check(await _users.CreateAsync(user, password));
            Check(await _users.AddToRoleAsync(user, "Admin"));
            await RegisterAsync(ct);
            await GrantAdminAsync(ct);
        }
        else
        {
            if (args.Contains("--register-capabilities")) await RegisterAsync(ct);
            if (args.Contains("--grant-admin-capabilities")) await GrantAdminAsync(ct);
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        Console.WriteLine("Explicit bootstrap/registration completed. HTTP startup remains read-only to ACL state.");
    }

    private async Task RegisterAsync(CancellationToken ct)
    {
        foreach (var definition in Manifest())
        {
            var function = await _db.Functions.SingleOrDefaultAsync(f => f.NormalizedCode == definition.Function.ToUpperInvariant(), ct);
            if (function is null)
            {
                function = new Function { Code = definition.Function, NormalizedCode = definition.Function.ToUpperInvariant(), Name = definition.Function };
                _db.Functions.Add(function);
            }
            foreach (var code in definition.Actions)
            {
                var action = _db.Actions.Local.FirstOrDefault(a => a.NormalizedCode == code.ToUpperInvariant())
                    ?? await _db.Actions.SingleOrDefaultAsync(a => a.NormalizedCode == code.ToUpperInvariant(), ct);
                if (action is null) { action = new Domain.Identity.Action(code); _db.Actions.Add(action); }
                if (!await _db.ActionInFunctions.AnyAsync(p => p.FunctionId == function.Id && p.ActionId == action.Id, ct) &&
                    !_db.ActionInFunctions.Local.Any(p => p.FunctionId == function.Id && p.ActionId == action.Id))
                    _db.ActionInFunctions.Add(new ActionInFunction(action.Id, function.Id));
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task GrantAdminAsync(CancellationToken ct)
    {
        var admin = await _roles.FindByNameAsync("Admin") ?? throw new ConflictException("Admin role is missing.");
        var changed = false;
        var before = await _db.Permissions.Where(p => p.RoleId == admin.Id)
            .Select(p => new { p.FunctionId, p.ActionId }).ToListAsync(ct);
        foreach (var definition in Manifest())
        {
            var function = await _db.Functions.SingleAsync(f => f.NormalizedCode == definition.Function.ToUpperInvariant(), ct);
            foreach (var code in definition.Actions)
            {
                var action = await _db.Actions.SingleAsync(a => a.NormalizedCode == code.ToUpperInvariant(), ct);
                var pair = await _db.ActionInFunctions.SingleAsync(p => p.FunctionId == function.Id && p.ActionId == action.Id, ct);
                if (!function.IsActive || !action.IsActive || !pair.IsActive) continue;
                var grant = await _db.Permissions.IgnoreQueryFilters().SingleOrDefaultAsync(p =>
                    p.RoleId == admin.Id && p.FunctionId == function.Id && p.ActionId == action.Id, ct);
                if (grant is null)
                {
                    _db.Permissions.Add(new Permission(admin.Id, function.Id, action.Id));
                    changed = true;
                }
                else if (grant.DeletedOn is not null)
                {
                    grant.DeletedOn = null; grant.DeletedBy = null; changed = true;
                }
            }
        }
        if (changed)
        {
            // CLI updates participate in the same optimistic concurrency contract as HTTP updates.
            Check(await _roles.UpdateAsync(admin));
            await _db.SaveChangesAsync(ct);
            var after = await _db.Permissions.Where(p => p.RoleId == admin.Id)
                .Select(p => new { p.FunctionId, p.ActionId }).ToListAsync(ct);
            _db.Trails.Add(new Domain.Auditing.Trail
            {
                UserId = Guid.Empty, TableName = "Roles", PrimaryKey = admin.Id,
                Type = Domain.Auditing.TrailType.Update, DateTime = DateTime.UtcNow,
                OldValues = JsonSerializer.Serialize(new { Source = "OperatorGrantAdmin", Grants = before }),
                NewValues = JsonSerializer.Serialize(new { Source = "OperatorGrantAdmin", Grants = after })
            });
        }
    }

    public Task PreflightAsync(CancellationToken ct = default) => FoundationPreflight.RunAsync(_db, ct);
}
