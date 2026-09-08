# Eizo.Playback

Playback abstraction and LibVLC adapter layer for **Eizo**.

## Goals

- Keep Eizo's WinUI 3 UI independent from LibVLC types.
- Provide a stable playback contract for local and future remote media sources.
- Keep backend-specific code isolated so another backend can be added later without rewriting the UI.
- Ship with automated build and contract tests from the start.

## Repository layout

```text
src/
  Eizo.Playback.Abstractions/       Public playback contracts and models
  Eizo.Playback.Core/               Backend-neutral playback state and orchestration
  Eizo.Playback.LibVLC/             LibVLCSharp + LibVLC Windows adapter
  Eizo.Playback.LibVLC.WinUI/       WinUI 3 video-surface bootstrap

tests/
  Eizo.Playback.Core.Tests/         Backend-neutral unit tests
  Eizo.Playback.LibVLC.Tests/       Windows/native LibVLC integration tests

docs/
  architecture.md
  chapter-navigation.md
  dependencies.md
  playback-contract.md
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

**Stage 4 — title/chapter navigation.**

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
- title discovery and selection
- chapter discovery and selection
- chapter name, start, duration and end time
- next/previous chapter navigation
- LibVLC title/chapter change tracking
- Windows/native integration tests
- NuGet pack validation

Not yet included:

- automatic OP/ED semantic classification
- media diagnostics
- application player chrome

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore Eizo.Playback.slnx
dotnet build Eizo.Playback.slnx -c Release
dotnet test Eizo.Playback.slnx -c Release
```
