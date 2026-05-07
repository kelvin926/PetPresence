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

    tests = require_file("tests/PetPresence.Tests/Program.cs")
    for token in ["ClassifiesWordAsWriting", "ClassifiesYouTubeAsWatching", "NormalModeIsClickThrough", "PresenceDtoDoesNotExposeRawMetadata"]:
        require(token in tests, f"v0 test missing {token}")

    verify_privacy_contracts()
    scan_source_for_banned_apis()


def verify_v1() -> None:
    verify_v0()
    hub = require_file("src/PetPresence.Server/Hubs/PresenceHub.cs")
    for token in ["UpdatePresence", "FriendPresenceChanged", "Clients.Others", "ConnectionClosed", "user:"]:
        require(token in hub, f"v1 PresenceHub missing {token}")
    auth = require_file("src/PetPresence.Server/Auth/DevelopmentUserContext.cs")
    require("X-User-Id" in auth, "v1 development auth must use X-User-Id")
    store = require_file("src/PetPresence.Server/Presence/PresenceStore.cs")
    require("expiresAt" in store and "TimeSpan" in store, "v1 presence TTL store missing")
    client = require_file("src/PetPresence.Desktop/Presence/PresenceClient.cs")
    require("HubConnectionBuilder" in client and "UpdatePresence" in client, "v1 desktop SignalR client missing")
    tests = require_file("tests/PetPresence.Tests/Program.cs")
    for token in ["PresenceDtoRejectsSenderMismatch", "PresenceTtlExpiresSnapshot"]:
        require(token in tests, f"v1 test missing {token}")
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
    tests = require_file("tests/PetPresence.Tests/Program.cs")
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
    tests = require_file("tests/PetPresence.Tests/Program.cs")
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
    tests = require_file("tests/PetPresence.Tests/Program.cs")
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
