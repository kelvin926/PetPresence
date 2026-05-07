using PetPresence.Contracts;

namespace PetPresence.Server.Presence;

public static class PresenceUpdateValidator
{
    private static readonly HashSet<string> AllowedAnimationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "idle", "typing", "watching", "browsing", "listening", "away", "offline", "gaming"
    };

    public static PresenceUpdateDto ValidateCallerCanSend(string callerUserId, PresenceUpdateDto update)
    {
        if (!string.Equals(callerUserId, update.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Presence sender mismatch.");
        }

        if (update.StatusText.Length > 64)
        {
            throw new InvalidOperationException("Presence status text is too long.");
        }

        if (!AllowedAnimationKeys.Contains(update.AnimationKey))
        {
            throw new InvalidOperationException("Unknown animation key.");
        }

        var confidence = Math.Clamp(update.Confidence, 0, 1);
        return update with
        {
            UserId = callerUserId,
            Confidence = confidence,
            UpdatedAt = update.UpdatedAt == default ? DateTimeOffset.UtcNow : update.UpdatedAt
        };
    }
}
