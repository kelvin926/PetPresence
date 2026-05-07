using PetPresence.Contracts;

namespace PetPresence.Desktop.Activity;

public sealed class ActivityStabilizer
{
    private readonly TimeSpan _minimumStableDuration;
    private ActivityState? _candidate;
    private DateTimeOffset _candidateSince;
    private ActivityState _confirmed = ActivityState.Unknown;

    public ActivityStabilizer(TimeSpan minimumStableDuration)
    {
        if (minimumStableDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumStableDuration));
        }

        _minimumStableDuration = minimumStableDuration;
    }

    public ActivityState Update(ActivityState observed, DateTimeOffset now)
    {
        if (_candidate is null || _candidate.Kind != observed.Kind)
        {
            _candidate = observed;
            _candidateSince = now;
            return _confirmed;
        }

        if (now - _candidateSince >= _minimumStableDuration)
        {
            _confirmed = observed;
        }

        return _confirmed;
    }
}
