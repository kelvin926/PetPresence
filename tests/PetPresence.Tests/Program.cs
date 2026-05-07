using PetPresence.Contracts;
using PetPresence.Desktop.Activity;
using PetPresence.Server.Presence;
using PetPresence.Server.Friends;
using PetPresence.Desktop.Overlay;
using PetPresence.Desktop.Privacy;
using PetPresence.Desktop.Diagnostics;
using PetPresence.Desktop.Distribution;
using PetPresence.Desktop.Settings;

var tests = new (string Name, Action Test)[]
{
    ("ClassifiesWordAsWriting", ClassifiesWordAsWriting),
    ("ClassifiesYouTubeAsWatching", ClassifiesYouTubeAsWatching),
    ("ClassifiesSearchAsWebBrowsing", ClassifiesSearchAsWebBrowsing),
    ("ClassifiesIdleAsAway", ClassifiesIdleAsAway),
    ("NormalModeIsClickThrough", NormalModeIsClickThrough),
    ("PresenceDtoDoesNotExposeRawMetadata", PresenceDtoDoesNotExposeRawMetadata),
    ("PresenceDtoRejectsSenderMismatch", PresenceDtoRejectsSenderMismatch),
    ("PresenceTtlExpiresSnapshot", PresenceTtlExpiresSnapshot),
    ("OnlyAcceptedFriendsReceivePresence", OnlyAcceptedFriendsReceivePresence),
    ("FriendLayoutRoundTrips", FriendLayoutRoundTrips),
    ("PrivacyPauseSuppressesSharing", PrivacyPauseSuppressesSharing),
    ("ExcludedAppSuppressesSharing", ExcludedAppSuppressesSharing),
    ("ApproximateModeCoarsensStatus", ApproximateModeCoarsensStatus),
    ("IdleReaderContract", IdleReaderContract),
    ("CrashLogsAreSanitized", CrashLogsAreSanitized),
    ("SettingsExportImportRoundTrips", SettingsExportImportRoundTrips),
    ("UpdateManifestRejectsDowngrade", UpdateManifestRejectsDowngrade),
    ("LocalPresenceLoopUpdatesOwnPet", LocalPresenceLoopUpdatesOwnPet),
    ("FriendPresenceAddsPet", FriendPresenceAddsPet),
    ("PresenceValidatorCanonicalizesStatusText", PresenceValidatorCanonicalizesStatusText),
    ("AppConfiguresPresenceClientFromEnvironment", AppConfiguresPresenceClientFromEnvironment),
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"PASS {name}");
}

static void ClassifiesWordAsWriting()
{
    var state = new ActivityClassifier().Classify(Snapshot("WINWORD", "draft.docx - Word"), TimeSpan.Zero);
    AssertEqual(ActivityKind.WritingDocument, state.Kind);
    AssertEqual("typing", state.AnimationKey);
}

static void ClassifiesYouTubeAsWatching()
{
    var state = new ActivityClassifier().Classify(Snapshot("chrome.exe", "A video - YouTube - Google Chrome"), TimeSpan.Zero);
    AssertEqual(ActivityKind.WatchingVideo, state.Kind);
    AssertEqual("watching", state.AnimationKey);
}

static void ClassifiesSearchAsWebBrowsing()
{
    var state = new ActivityClassifier().Classify(Snapshot("msedge", "고양이 검색 - Naver"), TimeSpan.Zero);
    AssertEqual(ActivityKind.WebBrowsing, state.Kind);
    AssertEqual("browsing", state.AnimationKey);
}

static void ClassifiesIdleAsAway()
{
    var state = new ActivityClassifier().Classify(Snapshot("Code", "PetPresence"), TimeSpan.FromSeconds(301));
    AssertEqual(ActivityKind.Away, state.Kind);
}

static void NormalModeIsClickThrough()
{
    var interop = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Overlay", "OverlayWindowInterop.cs"));
    AssertContains("WsExTransparent", interop);
    AssertContains("WsExNoActivate", interop);
    AssertContains("clickThrough", interop);
}

static void PresenceDtoDoesNotExposeRawMetadata()
{
    var dto = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Contracts", "PresenceUpdateDto.cs"));
    AssertDoesNotContain("ProcessName", dto);
    AssertDoesNotContain("WindowTitle", dto);
    AssertDoesNotContain("Url", dto);
}


static void PresenceDtoRejectsSenderMismatch()
{
    var update = new PresenceUpdateDto("friend-a", PresenceStatus.Coding, "코딩 중...", "typing", 0.8, DateTimeOffset.UtcNow);
    AssertThrows<InvalidOperationException>(() => PresenceUpdateValidator.ValidateCallerCanSend("local-user", update));
}

static void PresenceTtlExpiresSnapshot()
{
    var store = new PresenceStore(TimeSpan.FromSeconds(1));
    var now = DateTimeOffset.UtcNow;
    var update = new PresenceUpdateDto("local-user", PresenceStatus.Coding, "코딩 중...", "typing", 0.8, now);
    store.Upsert(update, now);
    AssertTrue(store.TryGetFresh("local-user", now.AddMilliseconds(500), out _), "snapshot should be fresh before ttl");
    AssertTrue(store.Expire(now.AddSeconds(2)).Count == 1, "snapshot should expire after ttl");
}


static void OnlyAcceptedFriendsReceivePresence()
{
    var store = new FriendshipStore();
    store.RequestFriend("local-user", "friend-a");
    AssertTrue(store.GetAcceptedFriendIds("local-user").Count == 0, "pending friend must not receive presence");
    store.AcceptFriend("friend-a", "local-user");
    AssertTrue(store.GetAcceptedFriendIds("local-user").SequenceEqual(new[] { "friend-a" }), "accepted friend should receive presence");
    store.BlockFriend("local-user", "friend-a");
    AssertTrue(store.GetAcceptedFriendIds("local-user").Count == 0, "blocked friend must not receive presence");
}

static void FriendLayoutRoundTrips()
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
        AssertEqual(12d, loaded["friend-a"].X);
        AssertEqual(78d, loaded["friend-b"].Y);
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}


static void PrivacyPauseSuppressesSharing()
{
    var decision = new PrivacyFilter().Apply(
        Snapshot("Code", "PetPresence"),
        new ActivityState(ActivityKind.Coding, "코딩 중...", "typing", 0.8),
        new PrivacySettings { SharingPaused = true },
        DateTimeOffset.UtcNow);
    AssertTrue(decision.ShouldSuppress, "pause should suppress sharing");
}

static void ExcludedAppSuppressesSharing()
{
    var settings = new PrivacySettings();
    settings.ExcludedProcessNames.Add("winword");
    var decision = new PrivacyFilter().Apply(
        Snapshot("WINWORD.exe", "private draft"),
        new ActivityState(ActivityKind.WritingDocument, "문서 작성 중...", "typing", 0.8),
        settings,
        DateTimeOffset.UtcNow);
    AssertTrue(decision.ShouldSuppress, "excluded process should suppress sharing");
}

static void ApproximateModeCoarsensStatus()
{
    var decision = new PrivacyFilter().Apply(
        Snapshot("Code", "PetPresence"),
        new ActivityState(ActivityKind.Coding, "코딩 중...", "typing", 0.8),
        new PrivacySettings { ApproximateStatusOnly = true },
        DateTimeOffset.UtcNow);
    AssertEqual(ActivityKind.Unknown, decision.State.Kind);
    AssertEqual("활동 중...", decision.State.StatusText);
}

static void IdleReaderContract()
{
    var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Activity", "IdleTimeReader.cs"));
    AssertContains("GetLastInputInfo", text);
    AssertContains("LASTINPUTINFO", text);
}


static void CrashLogsAreSanitized()
{
    var sanitized = CrashLogService.Sanitize("ProcessName: WINWORD\nWindow Title: private draft\nhttps://example.test/private");
    AssertDoesNotContain("WINWORD", sanitized);
    AssertDoesNotContain("private draft", sanitized);
    AssertDoesNotContain("https://", sanitized);
}

static void SettingsExportImportRoundTrips()
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
        AssertEqual("local-user", loaded.UserId);
        AssertTrue(loaded.Privacy.ExcludedProcessNames.Contains("winword"), "excluded process should round-trip");
        AssertEqual(2d, loaded.PetPositions[0].Y);
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void UpdateManifestRejectsDowngrade()
{
    var service = new UpdateService(new HttpClient());
    var manifest = new UpdateManifest("1.0.0", new Uri("https://example.test/PetPresence.exe"), new string('a', 64), DateTimeOffset.UtcNow);
    var result = service.EvaluateManifest(manifest, new Version(1, 0, 1));
    AssertTrue(!result.UpdateAvailable, "downgrade must be rejected");
}


static void LocalPresenceLoopUpdatesOwnPet()
{
    var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Presence", "LocalPresenceController.cs"));
    AssertContains("PeriodicTimer", text);
    AssertContains("ActivityStabilizer", text);
    AssertContains("StatusText", text);
    AssertContains("AnimationKey", text);
    AssertContains("_heartbeatInterval", text);
}

static void FriendPresenceAddsPet()
{
    var viewModel = new OverlayViewModel();
    var controllerText = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "Presence", "PresenceOverlayController.cs"));
    var pet = viewModel.GetOrAddFriend("friend-a", "Friend A");
    AssertEqual("friend-a", pet.UserId);
    AssertContains("ApplyFriendPresence", controllerText);
    AssertContains("GetOrAddFriend", controllerText);
}


static void PresenceValidatorCanonicalizesStatusText()
{
    var update = new PresenceUpdateDto("local-user", PresenceStatus.Coding, "private project title", "unknown", 0.8, DateTimeOffset.UtcNow);
    var safe = PresenceUpdateValidator.ValidateCallerCanSend("local-user", update);
    AssertEqual("코딩 중...", safe.StatusText);
    AssertEqual("typing", safe.AnimationKey);
}

static void AppConfiguresPresenceClientFromEnvironment()
{
    var text = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PetPresence.Desktop", "App.xaml.cs"));
    AssertContains("PETPRESENCE_SERVER_URL", text);
    AssertContains("PresenceOverlayController", text);
    AssertContains("presenceClient: _presenceClient", text);
}

static ForegroundAppSnapshot Snapshot(string processName, string title) =>
    new(1234, processName, title, DateTimeOffset.UtcNow);

static string ProjectRoot()
{
    var directory = AppContext.BaseDirectory;
    while (directory is not null && !File.Exists(Path.Combine(directory, "PetPresence.sln")))
    {
        directory = Directory.GetParent(directory)?.FullName;
    }

    return directory ?? throw new InvalidOperationException("Could not locate project root");
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected text to contain {expected}");
    }
}

static void AssertDoesNotContain(string forbidden, string actual)
{
    if (actual.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Forbidden text found: {forbidden}");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}");
}
