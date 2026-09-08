# Eizo.Playback

Playback abstraction and LibVLC adapter layer for **Eizo**.

## Goals

- Keep Eizo's WinUI 3 UI independent from LibVLC types.
- Provide a stable playback contract for local and future remote media sources.
- Keep backend-specific code isolated so another backend can be added later without rewriting the UI.
- Ship with automated build, compatibility and contract tests from the start.

## Repository layout

```text
src/
  Eizo.Playback.Abstractions/       Public playback contracts and models
  Eizo.Playback.Core/               Backend-neutral playback state and orchestration
  Eizo.Playback.LibVLC/             LibVLCSharp + LibVLC Windows adapter
  Eizo.Playback.LibVLC.WinUI/       WinUI 3 video-surface bootstrap

tests/
  Eizo.Playback.Core.Tests/         Backend-neutral unit tests
  Eizo.Playback.LibVLC.Tests/       Windows/native LibVLC + API-boundary tests

docs/
  architecture.md
  chapter-navigation.md
  compatibility-matrix.md
  dependencies.md
  playback-contract.md
  playback-diagnostics.md
  release-checklist.md
  track-management.md
  winui-integration.md
```

## Dependency direction

```text
Eizo UI
   |
   v
Eizo.Playback.LibVLC.WinUI
   |
   +--> Eizo.Playback.Abstractions
   |
   +--> Eizo.Playback.LibVLC
              |
              v
         LibVLCSharp
              |
              v
            LibVLC
```

The Eizo application does not need to reference LibVLCSharp directly.

## Current status

**Stage 6 — compatibility and release hardening.**

Implemented:

- backend initialization
- local/URI media open
- play / pause / stop / seek
- normalized volume and playback rate
- position and duration
- playback state and failure events
- WinUI 3 `PlaybackView`
- D3D11 swap-chain bootstrap for LibVLC 3
- audio/video/subtitle track discovery and selection
- external subtitles and A/V subtitle delay
- title/chapter discovery and navigation
- chapter timing
- backend/runtime diagnostics
- privacy-safe input diagnostics
- native Windows integration tests
- public API isolation tests
- CI-backed compatibility matrix
- NuGet README and repository metadata
- CI package artifact retention
- release checklist
- NuGet pack validation

Current CI-verified media paths are deliberately narrower than LibVLC's full upstream capability set. See `docs/compatibility-matrix.md`.

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore Eizo.Playback.slnx
dotnet build Eizo.Playback.slnx -c Release
dotnet test Eizo.Playback.slnx -c Release
dotnet pack Eizo.Playback.slnx -c Release -o artifacts/packages
```

## Release note

The repository does not yet declare a project license. That decision is intentionally left to the repository owner rather than inferred from the LibVLC dependency stack.
