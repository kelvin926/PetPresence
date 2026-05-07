namespace PetPresence.Contracts;

public sealed record FriendDto(
    string UserId,
    string DisplayName,
    FriendshipStatus Status);

public sealed record FriendRequestDto(string FriendUserId);
