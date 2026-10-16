using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Boilerate.Infrastructure.Notifications.Hubs;
[Authorize]
public sealed class NotificationHub : Hub
{
    internal static string SessionGroup(Guid sessionId) => "session:" + sessionId.ToString("N");

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(Context.User.FindFirst("sid")?.Value, out var sessionId))
        {
            Context.Abort();
            return;
        }

        // Only the server chooses membership, from the JWT session already validated against DB.
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
