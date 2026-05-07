namespace PetPresence.Desktop.Distribution;

public sealed record UpdateManifest(
    string Version,
    Uri DownloadUri,
    string Sha256,
    DateTimeOffset PublishedAt);

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    UpdateManifest? Manifest,
    string Reason);
