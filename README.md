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
  Eizo.Playback.Abstractions/   Public playback contracts and models
  Eizo.Playback.Core/           Backend-neutral playback state and orchestration
  Eizo.Playback.LibVLC/         LibVLCSharp + LibVLC Windows adapter

tests/
  Eizo.Playback.Core.Tests/     Backend-neutral unit tests

docs/
  architecture.md
  dependencies.md
```

## Dependency direction

```text
Eizo UI
   |
   v
Eizo.Playback.Abstractions
   |
   +--> Eizo.Playback.Core
             |
             v
      Eizo.Playback.LibVLC
             |
             v
        LibVLCSharp
             |
             v
           LibVLC
```

The Eizo application must not reference LibVLCSharp directly.

## Current status

**Stage 0 — repository/bootstrap architecture.**

The first functional milestone will add the minimum LibVLC lifecycle required for open, play, pause, stop, seek, position and duration.

## Build

Requires the .NET 10 SDK.

```powershell
dotnet restore Eizo.Playback.slnx
dotnet build Eizo.Playback.slnx -c Release
dotnet test Eizo.Playback.slnx -c Release
```
