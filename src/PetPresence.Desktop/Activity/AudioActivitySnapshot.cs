namespace PetPresence.Desktop.Activity;

public sealed record AudioActivitySnapshot(
    int ProcessId,
    string ProcessName,
    float PeakValue,
    DateTimeOffset CapturedAt);

public interface IAudioSessionReader
{
    IReadOnlyList<AudioActivitySnapshot> ReadActiveSessions();
}
