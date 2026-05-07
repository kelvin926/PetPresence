using System.Collections.Concurrent;
using PetPresence.Contracts;

namespace PetPresence.Server.Presence;

public sealed class PresenceStore
{
    private readonly ConcurrentDictionary<string, PresenceSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _ttl;

    public PresenceStore(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromSeconds(75);
    }

    public PresenceSnapshot Upsert(PresenceUpdateDto update, DateTimeOffset now)
    {
        var expiresAt = now.Add(_ttl);
        var snapshot = new PresenceSnapshot(update, expiresAt);
        _snapshots[update.UserId] = snapshot;
        return snapshot;
    }

    public bool TryGetFresh(string userId, DateTimeOffset now, out PresenceUpdateDto update)
    {
        update = default!;
        if (!_snapshots.TryGetValue(userId, out var snapshot) || snapshot.ExpiresAt <= now)
        {
            return false;
        }

        update = snapshot.Update;
        return true;
    }

    public PresenceUpdateDto RemoveAsOffline(string userId, DateTimeOffset now)
    {
        _snapshots.TryRemove(userId, out _);
        return new PresenceUpdateDto(userId, PresenceStatus.Offline, "오프라인...", "offline", 1, now);
    }

    public IReadOnlyList<PresenceUpdateDto> Expire(DateTimeOffset now)
    {
        var offlineUpdates = new List<PresenceUpdateDto>();
        foreach (var (userId, snapshot) in _snapshots)
        {
            if (snapshot.ExpiresAt > now)
            {
                continue;
            }

            if (_snapshots.TryRemove(userId, out _))
            {
                offlineUpdates.Add(new PresenceUpdateDto(userId, PresenceStatus.Offline, "오프라인...", "offline", 1, now));
            }
        }

        return offlineUpdates;
    }
}

public sealed record PresenceSnapshot(PresenceUpdateDto Update, DateTimeOffset ExpiresAt);
