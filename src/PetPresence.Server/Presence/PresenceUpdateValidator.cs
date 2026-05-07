using PetPresence.Contracts;

namespace PetPresence.Server.Presence;

public static class PresenceUpdateValidator
{
    private static readonly Dictionary<PresenceStatus, (string Text, string Animation)> CanonicalPresence = new()
    {
        [PresenceStatus.Offline] = ("오프라인...", "offline"),
        [PresenceStatus.Away] = ("자리 비움...", "away"),
        [PresenceStatus.WebBrowsing] = ("웹 보는 중...", "browsing"),
        [PresenceStatus.WatchingVideo] = ("영상 보는 중...", "watching"),
        [PresenceStatus.WritingDocument] = ("문서 작성 중...", "typing"),
        [PresenceStatus.ListeningMusic] = ("음악 듣는 중...", "listening"),
        [PresenceStatus.Coding] = ("코딩 중...", "typing"),
        [PresenceStatus.Unknown] = ("상태 확인 중...", "idle")
    };

    public static PresenceUpdateDto ValidateCallerCanSend(string callerUserId, PresenceUpdateDto update)
    {
        if (!string.Equals(callerUserId, update.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Presence sender mismatch.");
        }

        if (!CanonicalPresence.TryGetValue(update.Status, out var canonical))
        {
            throw new InvalidOperationException("Unknown presence status.");
        }

        var confidence = Math.Clamp(update.Confidence, 0, 1);
        return update with
        {
            UserId = callerUserId,
            StatusText = canonical.Text,
            AnimationKey = canonical.Animation,
            Confidence = confidence,
            UpdatedAt = update.UpdatedAt == default ? DateTimeOffset.UtcNow : update.UpdatedAt
        };
    }
}
