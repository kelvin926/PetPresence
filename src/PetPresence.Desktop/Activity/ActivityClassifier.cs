using PetPresence.Contracts;

namespace PetPresence.Desktop.Activity;

public sealed class ActivityClassifier
{
    private static readonly string[] BrowserProcesses =
    [
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "whale"
    ];

    private static readonly string[] VideoTitleMarkers =
    [
        "youtube", "netflix", "twitch", "disney+", "disney plus", "tving", "티빙", "wavve", "웨이브"
    ];

    private static readonly string[] SearchTitleMarkers =
    [
        "google", "bing", "naver", "검색", "search", "duckduckgo"
    ];

    private static readonly Dictionary<string, ActivityState> ProcessRules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["winword"] = new(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.90),
        ["hwp"] = new(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.90),
        ["notion"] = new(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.82),
        ["obsidian"] = new(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.82),
        ["typora"] = new(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.82),
        ["code"] = new(ActivityKind.Coding, "코딩 중...", "typing", 0.86),
        ["devenv"] = new(ActivityKind.Coding, "코딩 중...", "typing", 0.86),
        ["pycharm64"] = new(ActivityKind.Coding, "코딩 중...", "typing", 0.86),
        ["rider64"] = new(ActivityKind.Coding, "코딩 중...", "typing", 0.86),
        ["spotify"] = new(ActivityKind.ListeningMusic, "음악 듣는 중...", "listening", 0.86),
        ["applemusic"] = new(ActivityKind.ListeningMusic, "음악 듣는 중...", "listening", 0.86),
        ["musicbee"] = new(ActivityKind.ListeningMusic, "음악 듣는 중...", "listening", 0.86),
        ["foobar2000"] = new(ActivityKind.ListeningMusic, "음악 듣는 중...", "listening", 0.86),
        ["steam"] = new(ActivityKind.Gaming, "게임 중...", "gaming", 0.70)
    };

    public ActivityState Classify(ForegroundAppSnapshot? snapshot, TimeSpan idleTime)
    {
        if (idleTime > TimeSpan.FromSeconds(300))
        {
            return ActivityState.Away;
        }

        if (snapshot is null)
        {
            return ActivityState.Unknown;
        }

        var process = NormalizeProcessName(snapshot.ProcessName);
        if (ProcessRules.TryGetValue(process, out var state))
        {
            return state;
        }

        if (BrowserProcesses.Contains(process, StringComparer.OrdinalIgnoreCase))
        {
            return ClassifyBrowserTitle(snapshot.WindowTitle);
        }

        return new ActivityState(ActivityKind.Unknown, "상태 확인 중...", "idle", 0.30);
    }

    public static string NormalizeProcessName(string processName)
    {
        var normalized = processName.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized.ToLowerInvariant();
    }

    private static ActivityState ClassifyBrowserTitle(string title)
    {
        var normalizedTitle = title.ToLowerInvariant();

        if (VideoTitleMarkers.Any(marker => normalizedTitle.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return new ActivityState(ActivityKind.WatchingVideo, "영상 보는 중...", "watching", 0.76);
        }

        if (SearchTitleMarkers.Any(marker => normalizedTitle.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return new ActivityState(ActivityKind.WebBrowsing, "웹서칭 중...", "browsing", 0.70);
        }

        return new ActivityState(ActivityKind.WebBrowsing, "웹 보는 중...", "browsing", 0.60);
    }
}
