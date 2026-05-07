# PetPresence v4 release checklist

## Build

```powershell
./scripts/package-windows.ps1 -Configuration Release -Runtime win-x64
```

## Required release gates

- `python3 scripts/verify_stage.py v4` passes.
- On Windows with .NET SDK installed, `dotnet build PetPresence.sln -c Release` passes.
- Overlay normal mode remains click-through and non-activating.
- Server DTOs contain classified presence only.
- Crash logs are sanitized before writing.
- Autostart is opt-in and writes only to HKCU `Run`.
- Update checks reject downgrades and non-HTTPS downloads.
- Settings export/import round-trips privacy settings and pet positions.

## Privacy release note

PetPresence does not share raw foreground process names, raw window titles, browser history, page text, screenshots, or keystrokes. Friend presence sharing is accepted-friend only.
