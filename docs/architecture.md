# Architecture

## Purpose

Eizo.Playback is a boundary between the Eizo application and the selected media backend.

The public application-facing API must remain independent from LibVLCSharp so UI and media-library work can continue in parallel with backend work.

## Project boundaries

### Eizo.Playback.Abstractions

Owns only stable public contracts and backend-neutral models.

Allowed dependencies: .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI, Eizo application/database types.

### Eizo.Playback.Core

Owns backend-neutral state and orchestration.

Allowed dependencies: Eizo.Playback.Abstractions and .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI and Eizo media-library domain models.

### Eizo.Playback.LibVLC

Owns all mapping to LibVLCSharp and the native LibVLC runtime.

This is the only current project allowed to reference LibVLCSharp.

## Dependency rule

```text
Eizo UI / Library
        |
        v
Eizo.Playback.Abstractions
        |
        v
Eizo.Playback.Core
        |
        v
Eizo.Playback.LibVLC
        |
        v
LibVLCSharp -> LibVLC
```

No LibVLC type may appear in public contracts exposed to the Eizo application.

## Planned milestones

1. Repository and contract baseline.
2. LibVLC lifecycle: initialize, open, play, pause, stop and dispose.
3. Position, duration, seek and event bridge.
4. Audio/subtitle track mapping.
5. Chapters and title mapping.
6. Diagnostics and error mapping.
7. WinUI video-surface integration behind an Eizo-owned boundary.
8. Contract tests and media compatibility suite.
