using System.Text.Json;
using PetPresence.Contracts;

namespace PetPresence.Desktop.Overlay;

public sealed class FriendPetLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _filePath;

    public FriendPetLayoutStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PetPresence",
            "pet-layout.json");
    }

    public async Task<IReadOnlyDictionary<string, PetPositionDto>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, PetPositionDto>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_filePath);
        var positions = await JsonSerializer.DeserializeAsync<List<PetPositionDto>>(stream, JsonOptions, cancellationToken)
            ?? [];
        return positions.ToDictionary(position => position.UserId, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(IEnumerable<FriendPetViewModel> pets, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var positions = pets
            .Select(pet => new PetPositionDto(pet.UserId, pet.X, pet.Y))
            .OrderBy(position => position.UserId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, positions, JsonOptions, cancellationToken);
    }
}
