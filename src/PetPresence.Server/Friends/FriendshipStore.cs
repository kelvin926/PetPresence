using System.Collections.Concurrent;
using PetPresence.Contracts;

namespace PetPresence.Server.Friends;

public sealed class FriendshipStore
{
    private readonly ConcurrentDictionary<FriendshipKey, FriendshipRecord> _friendships = new();

    public FriendshipRecord RequestFriend(string requesterId, string addresseeId)
    {
        EnsureDifferentUsers(requesterId, addresseeId);
        var key = FriendshipKey.From(requesterId, addresseeId);
        return _friendships.AddOrUpdate(
            key,
            _ => new FriendshipRecord(requesterId, addresseeId, FriendshipStatus.Pending, DateTimeOffset.UtcNow),
            (_, existing) => existing.Status == FriendshipStatus.Blocked
                ? existing
                : existing with { RequesterId = requesterId, AddresseeId = addresseeId, Status = FriendshipStatus.Pending });
    }

    public FriendshipRecord AcceptFriend(string addresseeId, string requesterId)
    {
        EnsureDifferentUsers(addresseeId, requesterId);
        var key = FriendshipKey.From(addresseeId, requesterId);
        var record = _friendships.GetOrAdd(
            key,
            _ => new FriendshipRecord(requesterId, addresseeId, FriendshipStatus.Pending, DateTimeOffset.UtcNow));

        if (record.Status == FriendshipStatus.Blocked)
        {
            return record;
        }

        var accepted = record with { Status = FriendshipStatus.Accepted };
        _friendships[key] = accepted;
        return accepted;
    }

    public FriendshipRecord BlockFriend(string ownerId, string otherUserId)
    {
        EnsureDifferentUsers(ownerId, otherUserId);
        var key = FriendshipKey.From(ownerId, otherUserId);
        var blocked = new FriendshipRecord(ownerId, otherUserId, FriendshipStatus.Blocked, DateTimeOffset.UtcNow);
        _friendships[key] = blocked;
        return blocked;
    }

    public IReadOnlyList<string> GetAcceptedFriendIds(string userId)
    {
        return _friendships.Values
            .Where(record => record.Status == FriendshipStatus.Accepted && record.Involves(userId))
            .Select(record => record.OtherUser(userId))
            .Where(friendId => !string.IsNullOrWhiteSpace(friendId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<FriendDto> GetFriends(string userId)
    {
        return _friendships.Values
            .Where(record => record.Involves(userId))
            .Select(record => new FriendDto(record.OtherUser(userId), record.OtherUser(userId), record.Status))
            .OrderBy(friend => friend.UserId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureDifferentUsers(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot create friendship with self.");
        }
    }
}

public sealed record FriendshipRecord(
    string RequesterId,
    string AddresseeId,
    FriendshipStatus Status,
    DateTimeOffset CreatedAt)
{
    public bool Involves(string userId) =>
        string.Equals(RequesterId, userId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(AddresseeId, userId, StringComparison.OrdinalIgnoreCase);

    public string OtherUser(string userId) =>
        string.Equals(RequesterId, userId, StringComparison.OrdinalIgnoreCase) ? AddresseeId : RequesterId;
}

public readonly record struct FriendshipKey(string Left, string Right)
{
    public static FriendshipKey From(string left, string right)
    {
        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase) <= 0
            ? new FriendshipKey(left, right)
            : new FriendshipKey(right, left);
    }
}
