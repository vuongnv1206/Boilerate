using System.Security.Claims;
using Boilerate.Application.Identity.Users;
using Boilerate.Infrastructure.Auth.Permissions;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

public sealed class PermissionTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task DatabaseDecisionOverridesTokenSnapshot(bool hasClaim, bool databaseAllows, bool expected)
    {
        const string permission = "Permissions.User.View";
        var userId = Guid.NewGuid().ToString();
        var service = Substitute.For<IUserService>();
        service.HasPermissionAsync(userId, permission, Arg.Any<CancellationToken>()).Returns(databaseAllows);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (hasClaim)
        {
            claims.Add(new Claim(AppClaims.Permission, permission));
        }

        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext(new[] { requirement },
            new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), null);
        await new PermissionAuthorizationHandler(service).HandleAsync(context);

        Assert.Equal(expected, context.HasSucceeded);
        await service.Received(1).HasPermissionAsync(userId, permission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnauthenticatedClaimsCannotAuthorize()
    {
        var service = Substitute.For<IUserService>();
        service.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var requirement = new PermissionRequirement("Permissions.User.View");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(AppClaims.Permission, requirement.Permission)
        }));
        var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);
        await new PermissionAuthorizationHandler(service).HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Empty(service.ReceivedCalls());
    }

    [Fact]
    public async Task DynamicPolicyRequiresAuthentication()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync("Permissions.User.View");

        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }
}