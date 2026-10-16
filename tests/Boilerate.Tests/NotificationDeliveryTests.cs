using System.Security.Claims;
using Boilerate.Application.Identity.Tokens;
using Boilerate.Application.Notifications;
using Boilerate.Domain.Identity;
using Boilerate.Domain.Notifications;
using Boilerate.Infrastructure.Notifications;
using Boilerate.Infrastructure.Notifications.Hubs;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class NotificationDeliveryTests
{
    [Fact]
    public async Task HubOnlyJoinsTheAuthenticatedSessionGroup()
    {
        var groups = Substitute.For<IGroupManager>();
        var missingSession = Substitute.For<HubCallerContext>();
        missingSession.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "Test")));
        using (var rejected = new NotificationHub { Context = missingSession, Groups = groups })
            await rejected.OnConnectedAsync();
        missingSession.Received(1).Abort();
        await groups.DidNotReceive().AddToGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        var sessionId = Guid.NewGuid();
        var valid = Substitute.For<HubCallerContext>();
        valid.ConnectionId.Returns("test-connection");
        valid.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sid", sessionId.ToString()) }, "Test")));
        using var hub = new NotificationHub { Context = valid, Groups = groups };
        await hub.OnConnectedAsync();
        await groups.Received(1).AddToGroupAsync("test-connection", NotificationHub.SessionGroup(sessionId), Arg.Any<CancellationToken>());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task DeliveryExcludesRevokedAndExpiredSessionsAndRetriesFailedTransport(string provider)
    {
        await using var fixture = new FoundationDatabase(provider); await fixture.CreateAsync();
        var user = new ApplicationUser { UserName = "recipient", Email = "recipient@example.test", IsActive = true, EmailConfirmed = true };
        const string password = "NotificationPassword!827452";
        using (var scope = fixture.Scope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        var tokens = new List<TokenResponse>();
        for (var i = 0; i < 3; i++)
        {
            using var scope = fixture.Scope();
            tokens.Add(await scope.ServiceProvider.GetRequiredService<ITokenService>().GetTokenAsync(new(user.Email!, password), "127.0.0.1", default));
        }
        Guid notificationId;
        using (var scope = fixture.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ITokenService>().RevokeSessionAsync(user.Id, tokens[0].SessionId, default);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.UserSessions.Where(s => s.Id == tokens[1].SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresOn, DateTime.UtcNow.AddMinutes(-1)));
            notificationId = await scope.ServiceProvider.GetRequiredService<INotificationService>().SendToUserAsync(Guid.Parse(user.Id), "Title", "Private", NotificationType.Info);
        }
        var proxy = Substitute.For<IClientProxy>();
        proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Simulated transport outage")), Task.CompletedTask);
        var clients = Substitute.For<IHubClients>();
        clients.Groups(Arg.Any<IReadOnlyList<string>>()).Returns(proxy);
        var hub = Substitute.For<IHubContext<NotificationHub>>(); hub.Clients.Returns(clients);
        var services = new ServiceCollection(); fixture.ConfigureServices(services); services.AddSingleton(hub);
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var dispatcher = new NotificationDispatcher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationDispatcher>.Instance);
        await Assert.ThrowsAsync<IOException>(() => dispatcher.DispatchPendingAsync(default));
        using (var scope = fixture.Scope())
            Assert.False((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.SingleAsync(n => n.Id == notificationId)).IsSent);
        await dispatcher.DispatchPendingAsync(default);
        clients.Received(2).Groups(Arg.Is<IReadOnlyList<string>>(g => g.Count == 1 && g[0] == NotificationHub.SessionGroup(tokens[2].SessionId)));
        clients.DidNotReceive().Users(Arg.Any<IReadOnlyList<string>>());
        using (var scope = fixture.Scope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications.SingleAsync(n => n.Id == notificationId)).IsSent);
    }
}
