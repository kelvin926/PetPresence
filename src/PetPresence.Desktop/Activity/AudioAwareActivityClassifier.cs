using PetPresence.Contracts;

namespace PetPresence.Desktop.Activity;

public sealed class AudioAwareActivityClassifier
{
    private static readonly string[] MusicProcesses = ["spotify", "applemusic", "musicbee", "foobar2000"];

    public ActivityState Merge(ActivityState foregroundState, IEnumerable<AudioActivitySnapshot> audioSessions)
    {
        if (foregroundState.Kind is ActivityKind.WritingDocument or ActivityKind.Coding or ActivityKind.WatchingVideo)
        {
            return foregroundState;
        }

        var activeMusic = audioSessions
            .Select(session => ActivityClassifier.NormalizeProcessName(session.ProcessName))
            .Any(process => MusicProcesses.Contains(process, StringComparer.OrdinalIgnoreCase));

        return activeMusic
            ? new ActivityState(ActivityKind.ListeningMusic, "음악 듣는 중...", "listening", Math.Max(foregroundState.Confidence, 0.62))
            : foregroundState;
    }
}
