using PetPresence.Contracts;
using PetPresence.Desktop.Activity;

namespace PetPresence.Desktop.Privacy;

public sealed class PrivacyFilter
{
    public PrivacyDecision Apply(
        ForegroundAppSnapshot? snapshot,
        ActivityState classifiedState,
        PrivacySettings settings,
        DateTimeOffset now)
    {
        if (settings.AlwaysAppearOffline)
        {
            return new PrivacyDecision(
                ShouldSuppress: false,
                State: new ActivityState(ActivityKind.Offline, "오프라인...", "offline", 1),
                Reason: nameof(settings.AlwaysAppearOffline));
        }

        if (ShouldSuppress(snapshot, settings, now, out var reason))
        {
            return new PrivacyDecision(true, ActivityState.Unknown, reason);
        }

        return new PrivacyDecision(false, ApplyApproximation(classifiedState, settings), "Allowed");
    }

    public bool ShouldSuppress(
        ForegroundAppSnapshot? snapshot,
        PrivacySettings settings,
        DateTimeOffset now,
        out string reason)
    {
        if (settings.SharingPaused)
        {
            reason = nameof(settings.SharingPaused);
            return true;
        }

        if (settings.QuietHours?.Contains(TimeOnly.FromDateTime(now.LocalDateTime)) == true)
        {
            reason = nameof(settings.QuietHours);
            return true;
        }

        if (snapshot is not null)
        {
            var normalized = ActivityClassifier.NormalizeProcessName(snapshot.ProcessName);
            if (settings.ExcludedProcessNames.Contains(normalized))
            {
                reason = nameof(settings.ExcludedProcessNames);
                return true;
            }
        }

        reason = "Allowed";
        return false;
    }

    public ActivityState ApplyApproximation(ActivityState state, PrivacySettings settings)
    {
        if (!settings.ApproximateStatusOnly)
        {
            return state;
        }

        return state.Kind switch
        {
            ActivityKind.Away => state,
            ActivityKind.Offline => state,
            ActivityKind.Unknown => state,
            _ => new ActivityState(ActivityKind.Unknown, "활동 중...", "idle", Math.Min(state.Confidence, 0.55))
        };
    }
}

public sealed record PrivacyDecision(bool ShouldSuppress, ActivityState State, string Reason);
