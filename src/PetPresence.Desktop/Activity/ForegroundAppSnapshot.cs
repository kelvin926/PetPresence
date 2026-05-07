namespace PetPresence.Desktop.Activity;

public sealed record ForegroundAppSnapshot(
    int ProcessId,
    string ProcessName,
    string WindowTitle,
    DateTimeOffset CapturedAt);
