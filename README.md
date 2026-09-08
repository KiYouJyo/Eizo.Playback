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
  Eizo.Playback.LibVLC.Tests/       Windows/native LibVLC smoke tests
  Eizo.Playback.LibVLC.WinUI.Tests/ WinUI bootstrap argument tests

docs/
  architecture.md
  dependencies.md
  playback-contract.md
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

**Stage 2 — WinUI video-surface integration.**

Implemented:

- backend initialization
- local/URI media open
- play / pause / stop
- seek
- normalized volume
- playback rate
- position and duration
- playback state bridge
- position / duration events
- asynchronous failure event
- backend-neutral error model
- deterministic native resource disposal
- WinUI 3 `PlaybackView`
- D3D11 swap-chain bootstrap for LibVLC 3
- engine recreation when the WinUI surface is recreated
- Windows LibVLC smoke tests
- NuGet pack validation

Not yet included:

- audio-track selection
- subtitle-track selection
- chapters
- media diagnostics
- application player chrome

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore Eizo.Playback.slnx
dotnet build Eizo.Playback.slnx -c Release
dotnet test Eizo.Playback.slnx -c Release
```
