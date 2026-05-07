using PetPresence.Contracts;
using PetPresence.Desktop.Privacy;

namespace PetPresence.Desktop.Settings;

public sealed class PetPresenceSettings
{
    public string UserId { get; set; } = "local-user";
    public Uri? ServerUri { get; set; }
    public bool AutoStartEnabled { get; set; }
    public PrivacySettings Privacy { get; set; } = new();
    public List<PetPositionDto> PetPositions { get; set; } = [];
}
