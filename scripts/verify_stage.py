#!/usr/bin/env python3
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def require_file(path: str) -> str:
    p = ROOT / path
    require(p.exists(), f"missing required file: {path}")
    return p.read_text(encoding="utf-8")


def read_tests_source() -> str:
    test_dir = ROOT / "tests" / "PetPresence.Tests"
    require(test_dir.exists(), "missing test project directory")
    sources = []
    for path in sorted(test_dir.rglob("*.cs")):
        sources.append(path.read_text(encoding="utf-8"))
    require(sources, "test project contains no C# source files")
    return "\n".join(sources)


def scan_source_for_banned_apis() -> None:
    banned = [
        "SetWindowsHookEx",
        "GetAsyncKeyState",
        "GetKeyboardState",
        "CopyFromScreen",
        "Graphics.CopyFromScreen",
        "PrintWindow",
        "BitBlt",
        "CreateRemoteThread",
        "WriteProcessMemory",
        "ReadProcessMemory",
        "UrlHistory",
        "History Provider Cache",
    ]
    source_files = list((ROOT / "src").rglob("*.cs"))
    for path in source_files:
        text = path.read_text(encoding="utf-8")
        for api in banned:
            require(api not in text, f"banned API {api} found in {path.relative_to(ROOT)}")


def verify_privacy_contracts() -> None:
    dto = require_file("src/PetPresence.Contracts/PresenceUpdateDto.cs")
    lowered = dto.lower()
    for forbidden in ["processname", "windowtitle", "url", "document", "browserhistory"]:
        require(forbidden not in lowered, f"PresenceUpdateDto leaks forbidden raw field: {forbidden}")


def verify_v0() -> None:
    require_file("README.md")
    require_file("PetPresence.sln")
    fg = require_file("src/PetPresence.Desktop/Activity/ForegroundWindowReader.cs")
    for token in ["GetForegroundWindow", "GetWindowThreadProcessId", "GetWindowText", "Environment.ProcessId"]:
        require(token in fg, f"v0 foreground reader missing {token}")

    classifier = require_file("src/PetPresence.Desktop/Activity/ActivityClassifier.cs")
    for token in ["idleTime > TimeSpan.FromSeconds(300)", "WritingDocument", "Coding", "ListeningMusic", "WatchingVideo", "WebBrowsing", "Unknown"]:
        require(token in classifier, f"v0 classifier missing {token}")
    for marker in ["youtube", "netflix", "twitch", "google", "naver"]:
        require(marker in classifier.lower(), f"v0 classifier missing browser marker {marker}")

    stabilizer = require_file("src/PetPresence.Desktop/Activity/ActivityStabilizer.cs")
    require("minimumStableDuration" in stabilizer and "_confirmed" in stabilizer, "v0 stabilizer missing stable-state gate")

    xaml = require_file("src/PetPresence.Desktop/Overlay/OverlayWindow.xaml")
    for token in ["AllowsTransparency=\"True\"", "WindowStyle=\"None\"", "Topmost=\"True\"", "ShowInTaskbar=\"False\"", "ItemsControl"]:
        require(token in xaml, f"v0 overlay xaml missing {token}")

    interop = require_file("src/PetPresence.Desktop/Overlay/OverlayWindowInterop.cs")
    for token in ["WsExTransparent", "WsExLayered", "WsExToolWindow", "WsExNoActivate", "clickThrough"]:
        require(token in interop, f"v0 overlay interop missing {token}")

    tray = require_file("src/PetPresence.Desktop/Overlay/TrayIconHost.cs")
    require("NotifyIcon" in tray and "Edit pet positions" in tray, "v0 tray edit mode missing")
    local_controller = require_file("src/PetPresence.Desktop/Presence/LocalPresenceController.cs")
    for token in ["PeriodicTimer", "ActivityStabilizer", "StatusText", "AnimationKey", "_heartbeatInterval", "_minimumBubbleDisplay"]:
        require(token in local_controller, f"v0 local presence controller missing {token}")

    tests = read_tests_source()
    for token in ["ClassifiesWordAsWriting", "ClassifiesYouTubeAsWatching", "NormalModeIsClickThrough", "PresenceDtoDoesNotExposeRawMetadata"]:
        require(token in tests, f"v0 test missing {token}")

    verify_privacy_contracts()
    scan_source_for_banned_apis()


def verify_v1() -> None:
    verify_v0()
    hub = require_file("src/PetPresence.Server/Hubs/PresenceHub.cs")
    for token in ["UpdatePresence", "FriendPresenceChanged", "ConnectionClosed", "user:"]:
        require(token in hub, f"v1 PresenceHub missing {token}")
    require("Clients.Others" in hub or "GetAcceptedFriendIds" in hub, "v1/v2 PresenceHub must notify other clients or accepted friends")
    auth = require_file("src/PetPresence.Server/Auth/DevelopmentUserContext.cs")
    require("X-User-Id" in auth, "v1 development auth must use X-User-Id")
    store = require_file("src/PetPresence.Server/Presence/PresenceStore.cs")
    require("expiresAt" in store and "TimeSpan" in store, "v1 presence TTL store missing")
    client = require_file("src/PetPresence.Desktop/Presence/PresenceClient.cs")
    require("HubConnectionBuilder" in client and "UpdatePresence" in client, "v1 desktop SignalR client missing")
    overlay_controller = require_file("src/PetPresence.Desktop/Presence/PresenceOverlayController.cs")
    require("FriendPresenceChanged" in overlay_controller and "GetOrAddFriend" in overlay_controller, "v1 friend presence overlay wiring missing")
    tests = read_tests_source()
    for token in ["PresenceDtoRejectsSenderMismatch", "PresenceTtlExpiresSnapshot", "LocalPresenceLoopUpdatesOwnPet", "FriendPresenceAddsPet", "PresenceValidatorCanonicalizesStatusText", "AppConfiguresPresenceClientFromEnvironment"]:
        require(token in tests, f"v1 test missing {token}")
    validator = require_file("src/PetPresence.Server/Presence/PresenceUpdateValidator.cs")
    require("CanonicalPresence" in validator and "StatusText = canonical.Text" in validator, "v1 server must canonicalize status text")
    app = require_file("src/PetPresence.Desktop/App.xaml.cs")
    require("PETPRESENCE_SERVER_URL" in app and "PresenceOverlayController" in app, "v1 app must optionally wire presence client to overlay")
    verify_privacy_contracts()
    scan_source_for_banned_apis()


def verify_v2() -> None:
    verify_v1()
    friends = require_file("src/PetPresence.Server/Friends/FriendshipStore.cs")
    for token in ["Accepted", "Blocked", "RequestFriend", "AcceptFriend", "BlockFriend", "GetAcceptedFriendIds"]:
        require(token in friends, f"v2 friendship store missing {token}")
    hub = require_file("src/PetPresence.Server/Hubs/PresenceHub.cs")
    require("GetAcceptedFriendIds" in hub and "Clients.Group" in hub and "Clients.Others" not in hub, "v2 hub must restrict to accepted friend groups")
    program = require_file("src/PetPresence.Server/Program.cs")
    for route in ["/friends/request", "/friends/accept", "/friends/block", "/friends"]:
        require(route in program, f"v2 endpoint missing {route}")
    layout = require_file("src/PetPresence.Desktop/Overlay/FriendPetLayoutStore.cs")
    require("SaveAsync" in layout and "LoadAsync" in layout, "v2 pet position persistence missing")
    tests = read_tests_source()
    for token in ["OnlyAcceptedFriendsReceivePresence", "FriendLayoutRoundTrips"]:
        require(token in tests, f"v2 test missing {token}")
    verify_privacy_contracts()
    scan_source_for_banned_apis()


def verify_v3() -> None:
    verify_v2()
    privacy = require_file("src/PetPresence.Desktop/Privacy/PrivacySettings.cs")
    for token in ["ExcludedProcessNames", "SharingPaused", "QuietHours", "ApproximateStatusOnly", "AlwaysAppearOffline"]:
        require(token in privacy, f"v3 privacy setting missing {token}")
    filter_text = require_file("src/PetPresence.Desktop/Privacy/PrivacyFilter.cs")
    for token in ["ShouldSuppress", "ApplyApproximation", "AlwaysAppearOffline"]:
        require(token in filter_text, f"v3 privacy filter missing {token}")
    idle = require_file("src/PetPresence.Desktop/Activity/IdleTimeReader.cs")
    require("GetLastInputInfo" in idle and "LASTINPUTINFO" in idle, "v3 idle detection missing Win32 GetLastInputInfo")
    audio = require_file("src/PetPresence.Desktop/Activity/WindowsAudioSessionReader.cs")
    for token in ["IAudioSessionManager2", "IAudioSessionEnumerator", "IAudioMeterInformation", "GetProcessId"]:
        require(token in audio, f"v3 audio session reader missing {token}")
    tests = read_tests_source()
    for token in ["PrivacyPauseSuppressesSharing", "ExcludedAppSuppressesSharing", "ApproximateModeCoarsensStatus", "IdleReaderContract"]:
        require(token in tests, f"v3 test missing {token}")
    verify_privacy_contracts()
    scan_source_for_banned_apis()


def verify_v4() -> None:
    verify_v3()
    for path in [
        "src/PetPresence.Desktop/Distribution/AutoStartService.cs",
        "src/PetPresence.Desktop/Distribution/UpdateService.cs",
        "src/PetPresence.Desktop/Diagnostics/CrashLogService.cs",
        "src/PetPresence.Desktop/Settings/SettingsImportExportService.cs",
        "packaging/windows/PetPresence.wxs",
        "scripts/package-windows.ps1",
        "docs/RELEASE.md",
    ]:
        require_file(path)
    autostart = read("src/PetPresence.Desktop/Distribution/AutoStartService.cs")
    require("CurrentUser" in autostart and "Run" in autostart, "v4 autostart must be HKCU opt-in")
    crash = read("src/PetPresence.Desktop/Diagnostics/CrashLogService.cs")
    require("Sanitize" in crash and "window title" in crash.lower(), "v4 crash log sanitizer missing")
    settings = read("src/PetPresence.Desktop/Settings/SettingsImportExportService.cs")
    require("ExportAsync" in settings and "ImportAsync" in settings, "v4 settings import/export missing")
    test_project = require_file("tests/PetPresence.Tests/PetPresence.Tests.csproj")
    for token in ["Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio", "IsTestProject"]:
        require(token in test_project, f"test project missing xUnit/dotnet test configuration {token}")
    require("<OutputType>Exe</OutputType>" not in test_project, "test project must not be console-only after xUnit conversion")
    tests = read_tests_source()
    require("[Fact]" in tests, "xUnit tests must use [Fact]")
    for token in ["CrashLogsAreSanitized", "SettingsExportImportRoundTrips", "UpdateManifestRejectsDowngrade"]:
        require(token in tests, f"v4 test missing {token}")
    verify_privacy_contracts()
    scan_source_for_banned_apis()


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: verify_stage.py v0|v1|v2|v3|v4", file=sys.stderr)
        return 2
    stage = argv[1].lower()
    checks = {
        "v0": verify_v0,
        "v1": verify_v1,
        "v2": verify_v2,
        "v3": verify_v3,
        "v4": verify_v4,
    }
    if stage not in checks:
        print(f"unknown stage: {stage}", file=sys.stderr)
        return 2
    checks[stage]()
    print(f"OK: {stage} verification passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
