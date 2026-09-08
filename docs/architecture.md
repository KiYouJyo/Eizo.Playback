# Architecture

## Purpose

Eizo.Playback is a boundary between the Eizo application and the selected media backend.

The public application-facing API remains independent from LibVLCSharp so UI and media-library work can continue in parallel with backend work.

## Project boundaries

### Eizo.Playback.Abstractions

Owns stable public contracts and backend-neutral models.

Allowed dependencies: .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI, Eizo application/database types.

The engine exposes media-track capabilities through `IPlaybackTrackController`, keeping the main playback lifecycle API compact.

### Eizo.Playback.Core

Owns backend-neutral state and orchestration.

Allowed dependencies: Eizo.Playback.Abstractions and .NET BCL.

Forbidden dependencies: LibVLCSharp, WinUI and Eizo media-library domain models.

The playback state machine uses atomic state storage because native backend callbacks may arrive on different threads.

### Eizo.Playback.LibVLC

Owns mapping to LibVLCSharp and the native LibVLC runtime.

This assembly exposes only backend-neutral public behavior. Its native `MediaPlayer` bridge is internal and is visible only to the WinUI integration assembly.

`LibVlcPlaybackEngine` owns one LibVLC instance and one MediaPlayer for its lifetime.

`LibVlcTrackController` owns the backend-specific mapping between LibVLC elementary streams and Eizo track models.

Track descriptions are enriched from the current `Media.Tracks` metadata when available.

LibVLC input handling is disabled so keyboard and mouse ownership remains with the Eizo application.

### Eizo.Playback.LibVLC.WinUI

Owns the WinUI 3 video surface.

LibVLC 3 requires the D3D11 context and swap-chain pointers when the native LibVLC instance is created. Those pointers do not exist until LibVLCSharp's WinUI `VideoView` has initialized.

Therefore the WinUI layer creates the playback engine after `VideoView.Initialized`.

The application receives only `IPlaybackEngine` through `PlaybackView.Engine` and `PlaybackView.EngineChanged`.

## Dependency rule

```text
Eizo UI / Library
        |
        v
Eizo.Playback.LibVLC.WinUI
        |
        +----------------------------+
        |                            |
        v                            v
Eizo.Playback.Abstractions   Eizo.Playback.LibVLC
                                     |
                                     v
                                LibVLCSharp
                                     |
                                     v
                                   LibVLC
```

No LibVLCSharp type appears in the public application-facing surface contract.

## Surface lifecycle

The WinUI `VideoView` owns a D3D11 swap chain and destroys it when unloaded.

A LibVLC 3 instance initialized against one swap chain must not continue to be used after that swap chain is destroyed.

Consequently:

1. `PlaybackView` receives the live swap-chain options.
2. It creates a `LibVlcPlaybackEngine` with those options.
3. It exposes that engine to the Eizo application.
4. When the view unloads, the engine is disposed.
5. If the view later receives a new swap chain, a new engine is created.

The UI should treat the engine as scoped to the currently live `PlaybackView`.

## Track lifecycle

LibVLC track descriptions are most complete once an input is active.

The track controller refreshes automatically on:

- playback start
- elementary stream added
- elementary stream deleted
- elementary stream selected

The application can also request an explicit refresh.

Negative LibVLC pseudo-track IDs are not exposed. Nullable selected-track IDs represent disabled/unselected tracks.

## Event threading

LibVLC playback and track callbacks are translated into backend-neutral events, but those events intentionally do not claim WinUI thread affinity.

The application must marshal playback and track events before changing UI-bound state.

`PlaybackView.EngineChanged` and `PlaybackView.InitializationFailed` are surface lifecycle events and normally originate on the WinUI thread.

## Planned milestones

1. Repository and contract baseline. **Complete**
2. LibVLC lifecycle, position/duration, seek and event bridge. **Complete**
3. WinUI video-surface integration behind an Eizo-owned boundary. **Complete**
4. Audio/video/subtitle track mapping. **Stage 3**
5. Chapters and title mapping.
6. Diagnostics and error mapping.
7. Contract tests and media compatibility suite.
