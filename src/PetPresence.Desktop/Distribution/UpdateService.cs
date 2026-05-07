using System.Net.Http;
using System.Net.Http.Json;

namespace PetPresence.Desktop.Distribution;

public sealed class UpdateService
{
    private readonly HttpClient _httpClient;

    public UpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckAsync(Uri manifestUri, Version currentVersion, CancellationToken cancellationToken = default)
    {
        if (manifestUri.Scheme is not ("https" or "http"))
        {
            return new UpdateCheckResult(false, null, "Unsupported manifest scheme.");
        }

        var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(manifestUri, cancellationToken);
        if (manifest is null)
        {
            return new UpdateCheckResult(false, null, "Manifest missing.");
        }

        return EvaluateManifest(manifest, currentVersion);
    }

    public UpdateCheckResult EvaluateManifest(UpdateManifest manifest, Version currentVersion)
    {
        if (!Version.TryParse(manifest.Version, out var offeredVersion))
        {
            return new UpdateCheckResult(false, null, "Invalid update version.");
        }

        if (offeredVersion <= currentVersion)
        {
            return new UpdateCheckResult(false, null, "Downgrade or same-version update rejected.");
        }

        if (manifest.DownloadUri.Scheme != "https")
        {
            return new UpdateCheckResult(false, null, "Update downloads must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Sha256) || manifest.Sha256.Length != 64)
        {
            return new UpdateCheckResult(false, null, "Update manifest must include a SHA-256 checksum.");
        }

        return new UpdateCheckResult(true, manifest, "Update available.");
    }
}
