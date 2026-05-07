using PetPresence.Contracts;

namespace PetPresence.Desktop.Presence;

public static class PresenceMapping
{
    public static PresenceStatus ToPresenceStatus(ActivityKind kind) => kind switch
    {
        ActivityKind.Away => PresenceStatus.Away,
        ActivityKind.WebBrowsing => PresenceStatus.WebBrowsing,
        ActivityKind.WatchingVideo => PresenceStatus.WatchingVideo,
        ActivityKind.WritingDocument => PresenceStatus.WritingDocument,
        ActivityKind.ListeningMusic => PresenceStatus.ListeningMusic,
        ActivityKind.Coding => PresenceStatus.Coding,
        ActivityKind.Offline => PresenceStatus.Offline,
        _ => PresenceStatus.Unknown
    };
}
