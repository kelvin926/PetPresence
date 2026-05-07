using Microsoft.AspNetCore.SignalR;
using PetPresence.Contracts;
using PetPresence.Server.Auth;
using PetPresence.Server.Presence;

namespace PetPresence.Server.Hubs;

public sealed class PresenceHub : Hub
{
    private const string ConnectionClosed = nameof(ConnectionClosed);
    private readonly DevelopmentUserContext _userContext;
    private readonly PresenceStore _presenceStore;

    public PresenceHub(DevelopmentUserContext userContext, PresenceStore presenceStore)
    {
        _userContext = userContext;
        _presenceStore = presenceStore;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _userContext.GetRequiredUserId(Context);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    public async Task UpdatePresence(PresenceUpdateDto update)
    {
        var callerUserId = _userContext.GetRequiredUserId(Context);
        PresenceUpdateDto safeUpdate;
        try
        {
            safeUpdate = PresenceUpdateValidator.ValidateCallerCanSend(callerUserId, update);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message, ex);
        }

        _presenceStore.Upsert(safeUpdate, DateTimeOffset.UtcNow);

        // v1 MVP intentionally broadcasts to all connected clients except the sender.
        // v2 replaces this with accepted-friend-only groups.
        await Clients.Others.SendAsync("FriendPresenceChanged", safeUpdate);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _userContext.GetRequiredUserId(Context);
        var offline = _presenceStore.RemoveAsOffline(userId, DateTimeOffset.UtcNow);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        await Clients.Others.SendAsync("FriendPresenceChanged", offline, ConnectionClosed);
        await base.OnDisconnectedAsync(exception);
    }

    private static string UserGroup(string userId) => $"user:{userId}";
}
