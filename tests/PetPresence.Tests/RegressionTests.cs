using PetPresence.Contracts;
using PetPresence.Desktop.Activity;
using PetPresence.Desktop.Diagnostics;
using PetPresence.Desktop.Distribution;
using PetPresence.Desktop.Overlay;
using PetPresence.Desktop.Privacy;
using PetPresence.Desktop.Settings;
using PetPresence.Server.Friends;
using PetPresence.Server.Presence;
using Xunit;

namespace PetPresence.Tests;

public sealed class RegressionTests
{
    [Fact]
    public void ClassifiesWordAsWriting()
    {
        var state = new ActivityClassifier().Classify(Snapshot("WINWORD", "draft.docx - Word"), TimeSpan.Zero);
        Assert.Equal(ActivityKind.WritingDocument, state.Kind);
        Assert.Equal("typing", state.AnimationKey);
    }

    [Fact]
    public void ClassifiesYouTubeAsWatching()
    {
        var state = new ActivityClassifier().Classify(Snapshot("chrome.exe", "A video - YouTube - Google Chrome"), TimeSpan.Zero);
        Assert.Equal(ActivityKind.WatchingVideo, state.Kind);
        Assert.Equal("watching", state.AnimationKey);
    }

    [Fact]
    public void ClassifiesSearchAsWebBrowsing()
    {
        var state = new ActivityClassifier().Classify(Snapshot("msedge", "고양이 검색 - Naver"), TimeSpan.Zero);
        Assert.Equal(ActivityKind.WebBrowsing, state.Kind);
        Assert.Equal("browsing", state.AnimationKey);
    }

    [Fact]
    public void ClassifiesIdleAsAway()
    {
        var state = new ActivityClassifier().Classify(Snapshot("Code", "PetPresence"), TimeSpan.FromSeconds(301));
        Assert.Equal(ActivityKind.Away, state.Kind);
    }

    [Fact]
    public void NormalModeIsClickThrough()
    {
        var interop = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Overlay", "OverlayWindowInterop.cs"));
        Assert.Contains("WsExTransparent", interop, StringComparison.Ordinal);
        Assert.Contains("WsExNoActivate", interop, StringComparison.Ordinal);
        Assert.Contains("clickThrough", interop, StringComparison.Ordinal);
    }

    [Fact]
    public void PresenceDtoDoesNotExposeRawMetadata()
    {
        var dto = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Contracts", "PresenceUpdateDto.cs"));
        Assert.DoesNotContain("ProcessName", dto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WindowTitle", dto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Url", dto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PresenceDtoRejectsSenderMismatch()
    {
        var update = new PresenceUpdateDto("friend-a", PresenceStatus.Coding, "코딩 중...", "typing", 0.8, DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => PresenceUpdateValidator.ValidateCallerCanSend("local-user", update));
    }

    [Fact]
    public void PresenceTtlExpiresSnapshot()
    {
        var store = new PresenceStore(TimeSpan.FromSeconds(1));
        var now = DateTimeOffset.UtcNow;
        var update = new PresenceUpdateDto("local-user", PresenceStatus.Coding, "코딩 중...", "typing", 0.8, now);
        store.Upsert(update, now);
        Assert.True(store.TryGetFresh("local-user", now.AddMilliseconds(500), out _), "snapshot should be fresh before ttl");
        Assert.Single(store.Expire(now.AddSeconds(2)));
    }

    [Fact]
    public void OnlyAcceptedFriendsReceivePresence()
    {
        var store = new FriendshipStore();
        store.RequestFriend("local-user", "friend-a");
        Assert.Empty(store.GetAcceptedFriendIds("local-user"));
        store.AcceptFriend("friend-a", "local-user");
        Assert.Equal(new[] { "friend-a" }, store.GetAcceptedFriendIds("local-user"));
        store.BlockFriend("local-user", "friend-a");
        Assert.Empty(store.GetAcceptedFriendIds("local-user"));
    }

    [Fact]
    public void FriendLayoutRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petpresence-layout-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FriendPetLayoutStore(path);
            var pets = new[]
            {
                new FriendPetViewModel { UserId = "friend-a", DisplayName = "Friend A", X = 12, Y = 34 },
                new FriendPetViewModel { UserId = "friend-b", DisplayName = "Friend B", X = 56, Y = 78 },
            };
            store.SaveAsync(pets).GetAwaiter().GetResult();
            var loaded = store.LoadAsync().GetAwaiter().GetResult();
            Assert.Equal(12d, loaded["friend-a"].X);
            Assert.Equal(78d, loaded["friend-b"].Y);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void PrivacyPauseSuppressesSharing()
    {
        var decision = new PrivacyFilter().Apply(
            Snapshot("Code", "PetPresence"),
            new ActivityState(ActivityKind.Coding, "코딩 중...", "typing", 0.8),
            new PrivacySettings { SharingPaused = true },
            DateTimeOffset.UtcNow);
        Assert.True(decision.ShouldSuppress, "pause should suppress sharing");
    }

    [Fact]
    public void ExcludedAppSuppressesSharing()
    {
        var settings = new PrivacySettings();
        settings.ExcludedProcessNames.Add("winword");
        var decision = new PrivacyFilter().Apply(
            Snapshot("WINWORD.exe", "private draft"),
            new ActivityState(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.8),
            settings,
            DateTimeOffset.UtcNow);
        Assert.True(decision.ShouldSuppress, "excluded process should suppress sharing");
    }

    [Fact]
    public void ApproximateModeCoarsensStatus()
    {
        var decision = new PrivacyFilter().Apply(
            Snapshot("Code", "PetPresence"),
            new ActivityState(ActivityKind.Coding, "코딩 중...", "typing", 0.8),
            new PrivacySettings { ApproximateStatusOnly = true },
            DateTimeOffset.UtcNow);
        Assert.Equal(ActivityKind.Unknown, decision.State.Kind);
        Assert.Equal("활동 중...", decision.State.StatusText);
    }

    [Fact]
    public void IdleReaderContract()
    {
        var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Activity", "IdleTimeReader.cs"));
        Assert.Contains("GetLastInputInfo", text, StringComparison.Ordinal);
        Assert.Contains("LASTINPUTINFO", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CrashLogsAreSanitized()
    {
        var sanitized = CrashLogService.Sanitize("ProcessName: WINWORD\nWindow Title: private draft\nhttps://example.test/private");
        Assert.DoesNotContain("WINWORD", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private draft", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsExportImportRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petpresence-settings-{Guid.NewGuid():N}.json");
        try
        {
            var service = new SettingsImportExportService();
            var settings = new PetPresenceSettings
            {
                UserId = "local-user",
                AutoStartEnabled = true,
                PetPositions = [new PetPositionDto("friend-a", 1, 2)]
            };
            settings.Privacy.ExcludedProcessNames.Add("winword");
            service.ExportAsync(settings, path).GetAwaiter().GetResult();
            var loaded = service.ImportAsync(path).GetAwaiter().GetResult();
            Assert.Equal("local-user", loaded.UserId);
            Assert.Contains("winword", loaded.Privacy.ExcludedProcessNames);
            Assert.Equal(2d, loaded.PetPositions[0].Y);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void UpdateManifestRejectsDowngrade()
    {
        var service = new UpdateService(new HttpClient());
        var manifest = new UpdateManifest("1.0.0", new Uri("https://example.test/PetPresence.exe"), new string('a', 64), DateTimeOffset.UtcNow);
        var result = service.EvaluateManifest(manifest, new Version(1, 0, 1));
        Assert.False(result.UpdateAvailable, "downgrade must be rejected");
    }

    [Fact]
    public void LocalPresenceLoopUpdatesOwnPet()
    {
        var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Presence", "LocalPresenceController.cs"));
        Assert.Contains("PeriodicTimer", text, StringComparison.Ordinal);
        Assert.Contains("ActivityStabilizer", text, StringComparison.Ordinal);
        Assert.Contains("StatusText", text, StringComparison.Ordinal);
        Assert.Contains("AnimationKey", text, StringComparison.Ordinal);
        Assert.Contains("_heartbeatInterval", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FriendPresenceAddsPet()
    {
        var viewModel = new OverlayViewModel();
        var controllerText = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Presence", "PresenceOverlayController.cs"));
        var pet = viewModel.GetOrAddFriend("friend-a", "Friend A");
        Assert.Equal("friend-a", pet.UserId);
        Assert.Contains("ApplyFriendPresence", controllerText, StringComparison.Ordinal);
        Assert.Contains("GetOrAddFriend", controllerText, StringComparison.Ordinal);
    }

    [Fact]
    public void PresenceValidatorCanonicalizesStatusText()
    {
        var update = new PresenceUpdateDto("local-user", PresenceStatus.Coding, "private project title", "unknown", 0.8, DateTimeOffset.UtcNow);
        var safe = PresenceUpdateValidator.ValidateCallerCanSend("local-user", update);
        Assert.Equal("코딩 중...", safe.StatusText);
        Assert.Equal("typing", safe.AnimationKey);
    }

    [Fact]
    public void AppConfiguresPresenceClientFromEnvironment()
    {
        var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "App.xaml.cs"));
        Assert.Contains("PETPRESENCE_SERVER_URL", text, StringComparison.Ordinal);
        Assert.Contains("PresenceOverlayController", text, StringComparison.Ordinal);
        Assert.Contains("presenceClient: _presenceClient", text, StringComparison.Ordinal);
    }

    private static ForegroundAppSnapshot Snapshot(string processName, string title) =>
        new(1234, processName, title, DateTimeOffset.UtcNow);

    private static string ProjectRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "PetPresence.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory ?? throw new InvalidOperationException("Could not locate project root");
    }
}
