namespace PetPresence.Contracts;

/// <summary>
/// Server-safe presence payload. It intentionally contains only classified state,
/// never local foreground metadata or browsing history.
/// </summary>
public sealed record PresenceUpdateDto(
    string UserId,
    PresenceStatus Status,
    string StatusText,
    string AnimationKey,
    double Confidence,
    DateTimeOffset UpdatedAt);
