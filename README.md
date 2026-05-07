# PetPresence

PetPresence is a Windows-only desktop presence pet app: a small transparent overlay shows a pet for you and approved friends. Local foreground-window metadata is classified on the user's PC, and only privacy-preserving canonical presence values are shared.

## Hard constraints

- No keylogging or global keyboard/mouse hooks.
- No screen capture.
- No document body, chat content, browser history, or raw URL collection.
- No code injection into other processes.
- No input injection.
- No hidden surveillance behavior.
- The app only reads foreground window metadata using Win32 APIs in the desktop-side local classifier.
- Raw process names and window titles stay local and are never part of server/shared DTOs.
- The server receives only canonical presence status, canonical status text, animation key, confidence, timestamps, and identity fields required for friend routing.
- The overlay is topmost, non-activating, and click-through in normal mode.
- Overlay edit mode is the only mode that receives mouse input for pet positioning.

## Architecture

```text
src/PetPresence.Desktop      WPF overlay, foreground detector, local classifier
src/PetPresence.Server       ASP.NET Core SignalR presence server
src/PetPresence.Contracts    Shared DTOs and enums without raw metadata fields
tests/PetPresence.Tests      xUnit regression tests for classifier/privacy/server rules
scripts/verify_stage.py      Repo-native stage/privacy verifier
```

## Branch policy

- `main`: stable/buildable branch only.
- `dev`: default Codex working branch.
- `feature/*`: optional feature branches for larger isolated changes.

Do not force push and do not commit secrets.

## Version plan

- **v0**: local detection + own pet overlay, click-through normal mode, edit mode positioning.
- **v1**: SignalR presence server and one-friend sharing MVP.
- **v2**: accepted-friend authorization, multi-pet layout, saved positions.
- **v3**: privacy and accuracy controls: exclusions, pause, idle, audio-session detection.
- **v4**: installer/update/autostart/crash-log/settings import-export release hardening.

## Local verification

The test project is a proper xUnit project. `dotnet test` executes the regression tests and must fail on failed assertions.

Run on a Windows-capable .NET SDK environment:

```bash
dotnet --info
dotnet restore
dotnet build -c Debug
dotnet test -c Debug --no-build
```

Also run the repo-native stage/privacy verifier:

```bash
python3 scripts/verify_stage.py v0
python3 scripts/verify_stage.py v1
python3 scripts/verify_stage.py v2
python3 scripts/verify_stage.py v3
python3 scripts/verify_stage.py v4
```

If `python3` is unavailable, use `python` with the same arguments.

## CI expectations

GitHub Actions runs on `windows-latest` for pushes to `main`/`dev` and pull requests targeting `main`/`dev`. CI installs .NET 8 and Python, then runs:

```bash
dotnet restore
dotnet build -c Debug --no-restore
dotnet test -c Debug --no-build
python scripts/verify_stage.py v0
python scripts/verify_stage.py v1
python scripts/verify_stage.py v2
python scripts/verify_stage.py v3
python scripts/verify_stage.py v4
```

## Windows smoke test

CI proves restore/build/test/verifier behavior on `windows-latest`, but WPF overlay runtime behavior must still be checked in a real Windows desktop session. Use the manual guide and checklist in [docs/SMOKE_TEST.md](docs/SMOKE_TEST.md) before treating a release candidate as runtime-smoke-tested.
