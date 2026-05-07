using Microsoft.AspNetCore.SignalR;
using PetPresence.Contracts;
using PetPresence.Server.Auth;
using PetPresence.Server.Friends;
using PetPresence.Server.Presence;

namespace PetPresence.Server.Hubs;

public sealed class PresenceHub : Hub
{
    private const string ConnectionClosed = nameof(ConnectionClosed);
    private readonly DevelopmentUserContext _userContext;
    private readonly PresenceStore _presenceStore;
    private readonly FriendshipStore _friendshipStore;

    public PresenceHub(DevelopmentUserContext userContext, PresenceStore presenceStore, FriendshipStore friendshipStore)
    {
        _userContext = userContext;
        _presenceStore = presenceStore;
        _friendshipStore = friendshipStore;
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
        await SendToAcceptedFriends(callerUserId, safeUpdate, reason: "PresenceUpdated");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _userContext.GetRequiredUserId(Context);
        var offline = _presenceStore.RemoveAsOffline(userId, DateTimeOffset.UtcNow);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        await SendToAcceptedFriends(userId, offline, ConnectionClosed);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendToAcceptedFriends(string senderUserId, PresenceUpdateDto update, string reason)
    {
        foreach (var friendUserId in _friendshipStore.GetAcceptedFriendIds(senderUserId))
        {
            _ = reason;
            await Clients.Group(UserGroup(friendUserId)).SendAsync("FriendPresenceChanged", update);
        }
    }

    private static string UserGroup(string userId) => $"user:{userId}";
}
