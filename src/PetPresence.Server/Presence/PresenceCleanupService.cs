using Microsoft.AspNetCore.SignalR;
using PetPresence.Server.Friends;
using PetPresence.Server.Hubs;

namespace PetPresence.Server.Presence;

public sealed class PresenceCleanupService : BackgroundService
{
    private readonly PresenceStore _presenceStore;
    private readonly FriendshipStore _friendshipStore;
    private readonly IHubContext<PresenceHub> _hubContext;
    private readonly ILogger<PresenceCleanupService> _logger;

    public PresenceCleanupService(
        PresenceStore presenceStore,
        FriendshipStore friendshipStore,
        IHubContext<PresenceHub> hubContext,
        ILogger<PresenceCleanupService> logger)
    {
        _presenceStore = presenceStore;
        _friendshipStore = friendshipStore;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var expired = _presenceStore.Expire(DateTimeOffset.UtcNow);
            foreach (var update in expired)
            {
                _logger.LogInformation("Presence TTL expired for user {UserId}", update.UserId);
                foreach (var friendUserId in _friendshipStore.GetAcceptedFriendIds(update.UserId))
                {
                    await _hubContext.Clients.Group($"user:{friendUserId}").SendAsync("FriendPresenceChanged", update, "PresenceExpired", stoppingToken);
                }
            }
        }
    }
}
