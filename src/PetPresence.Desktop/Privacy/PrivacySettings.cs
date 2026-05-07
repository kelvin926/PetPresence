namespace PetPresence.Desktop.Privacy;

public sealed class PrivacySettings
{
    public bool SharingPaused { get; set; }
    public bool AlwaysAppearOffline { get; set; }
    public bool ApproximateStatusOnly { get; set; }
    public HashSet<string> ExcludedProcessNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public QuietHoursWindow? QuietHours { get; set; }
}

public sealed record QuietHoursWindow(TimeOnly Start, TimeOnly End)
{
    public bool Contains(TimeOnly localTime)
    {
        if (Start == End)
        {
            return true;
        }

        return Start < End
            ? localTime >= Start && localTime < End
            : localTime >= Start || localTime < End;
    }
}
