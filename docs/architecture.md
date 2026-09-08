# Architecture

## Purpose

Eizo.Playback is a boundary between the Eizo application and the selected media backend.

The public application-facing API must remain independent from LibVLCSharp so UI and media-library work can continue in parallel with backend work.

## Project boundaries

### Eizo.Playback.Abstractions

Owns only stable public contracts and backend-neutral models.

Allowed dependencies: .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI, Eizo application/database types.

Stage 1 public capabilities include playback lifecycle operations, normalized volume, playback rate, state, position, duration and backend-neutral errors.

### Eizo.Playback.Core

Owns backend-neutral state and orchestration.

Allowed dependencies: Eizo.Playback.Abstractions and .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI and Eizo media-library domain models.

The playback state machine uses atomic state storage because native backend callbacks may arrive on different threads.

### Eizo.Playback.LibVLC

Owns all mapping to LibVLCSharp and the native LibVLC runtime.

This is the only current project allowed to reference LibVLCSharp.

`LibVlcPlaybackEngine` owns one LibVLC instance and one MediaPlayer for its lifetime. The Eizo application should normally keep one engine alive for the lifetime of the player experience rather than creating a new native runtime for every episode.

LibVLC input handling is disabled so keyboard and mouse ownership remains with the Eizo UI.

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

## Event threading

LibVLC callbacks are translated into backend-neutral events, but those events intentionally do not claim WinUI thread affinity.

The future WinUI integration package or the Eizo UI must marshal callbacks through its dispatcher before changing UI-bound state.

## Planned milestones

1. Repository and contract baseline. **Complete**
2. LibVLC lifecycle, position/duration, seek and event bridge. **Stage 1**
3. WinUI video-surface integration behind an Eizo-owned boundary.
4. Audio/subtitle track mapping.
5. Chapters and title mapping.
6. Diagnostics and error mapping.
7. Contract tests and media compatibility suite.
