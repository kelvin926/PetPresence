# PetPresence

PetPresence is a Windows-only desktop presence pet app: a small transparent overlay shows a pet for you and, in later stages, approved friends. Local foreground-window metadata is classified on the user's PC, and only privacy-preserving status enums are shared.

## Hard constraints

- No keylogging or global keyboard/mouse hooks.
- No screen capture.
- No document body, chat content, browser history, or raw URL collection.
- No code injection into other processes.
- The app only reads foreground window metadata using Win32 APIs.
- Raw process names and window titles stay local by default and are never part of server presence DTOs.
- The overlay is topmost, non-activating, and click-through in normal mode.
- Overlay edit mode is the only mode that receives mouse input for pet positioning.

## Architecture

```text
src/PetPresence.Desktop      WPF overlay, foreground detector, local classifier
src/PetPresence.Server       ASP.NET Core SignalR presence server (added in v1)
src/PetPresence.Contracts    Shared DTOs and enums without raw metadata fields
tests/PetPresence.Tests      Lightweight regression tests for classifier/privacy rules
scripts/verify_stage.py      Repo-native verifier used when dotnet is unavailable
```

## Version plan

- **v0**: local detection + own pet overlay, click-through normal mode, edit mode positioning.
- **v1**: SignalR presence server and one-friend sharing MVP.
- **v2**: accepted-friend authorization, multi-pet layout, saved positions.
- **v3**: privacy and accuracy controls: exclusions, pause, idle, audio-session detection.
- **v4**: installer/update/autostart/crash-log/settings import-export release hardening.

## Local verification

This environment may not have the .NET SDK installed. Each stage therefore includes:

1. source-level tests in `tests/PetPresence.Tests`, intended for `dotnet run --project tests/PetPresence.Tests`; and
2. a Python static/verifier gate that runs in this workspace: `python3 scripts/verify_stage.py v0` (or `v1` ... `v4`).
