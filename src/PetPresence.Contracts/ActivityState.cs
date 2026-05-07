namespace PetPresence.Contracts;

public sealed record ActivityState(
    ActivityKind Kind,
    string StatusText,
    string AnimationKey,
    double Confidence)
{
    public static ActivityState Unknown { get; } = new(ActivityKind.Unknown, "상태 확인 중...", "idle", 0.20);
    public static ActivityState Away { get; } = new(ActivityKind.Away, "자리 비움...", "away", 0.95);
}
