using System.Text.Json;

namespace PetPresence.Desktop.Settings;

public sealed class SettingsImportExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task ExportAsync(PetPresenceSettings settings, string filePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
    }

    public async Task<PetPresenceSettings> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<PetPresenceSettings>(stream, JsonOptions, cancellationToken)
            ?? new PetPresenceSettings();
    }
}
