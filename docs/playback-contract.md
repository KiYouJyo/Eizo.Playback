# Playback contract

This document defines the application-facing behavior of `IPlaybackEngine`.

## Backend isolation

The public engine contract must never expose:

- `LibVLC`
- `MediaPlayer`
- `Media`
- `VLCState`
- `TrackDescription`
- `ChapterDescription`
- `MediaStats`
- any other LibVLCSharp type

The Eizo application may reference `Eizo.Playback.LibVLC.WinUI` to host video, but playback control continues through `IPlaybackEngine`.

## Lifecycle

A newly created engine starts in `Idle`.

```text
Idle
  |
  | OpenAsync
  v
Opening
  |
  v
Stopped
  |
  | PlayAsync
  v
Opening / Buffering
  |
  v
Playing <----> Paused
  |
  +---- Seeking ----+
  |
  +---- StopAsync --> Stopped
  |
  +---- media end --> Ended

Any asynchronous backend playback failure -> Failed
```

`OpenAsync` attaches a source but does not automatically start playback.

## WinUI surface ownership

On WinUI 3, `PlaybackView` owns creation of the LibVLC-backed engine because LibVLC 3 must receive the live swap-chain arguments at construction time.

The engine exposed through `PlaybackView.Engine` is valid only for the current live surface.

## Media tracks

Track operations are available through `IPlaybackEngine.Tracks`.

The public controller exposes backend-neutral audio, video and subtitle models.

A nullable selected-track ID means that track category is currently disabled or has no active selection.

## Title and chapter navigation

Navigation operations are available through `IPlaybackEngine.Navigation`.

The public controller exposes titles, chapters, current selections and adjacent chapter navigation.

Chapter start/duration values are nullable because not every backend/container exposes complete timing metadata.

## Diagnostics

Runtime information is available through `IPlaybackEngine.Diagnostics`.

The diagnostics controller exposes an immutable `PlaybackDiagnosticsSnapshot`.

The snapshot may contain:

- backend version and wrapper version
- input category and scheme
- seek/pause capabilities
- video-output count
- runtime FPS
- buffering percentage
- scrambled-program state
- media statistics
- currently selected track summaries
- hardware-decoding requested state
- hardware-decoding active state when provable

### Privacy

Diagnostics must not expose the complete input URI or local path.

This prevents credentials, query tokens, signed URLs or private filesystem locations from leaking into UI/logging surfaces.

### Hardware decoding

`HardwareDecodingRequested` describes configuration.

`HardwareDecodingActive` describes confirmed runtime state.

A backend must use `null` when it cannot prove active hardware decoding.

The LibVLC 3 adapter currently reports `null` for active state rather than inferring it.

## Position and duration

`Position` and `Duration` use `TimeSpan`.

Unknown or unavailable native values are normalized to `TimeSpan.Zero`.

`SeekAsync` rejects negative positions. If a known duration is available, seeks beyond the end are clamped to the duration.

## Volume

The public volume range is normalized:

```text
0.0 = silent
1.0 = 100%
```

## Playback rate

The public playback rate is a positive finite multiplier:

```text
1.0 = normal speed
0.5 = half speed
2.0 = double speed
```

## Events

The engine contract exposes:

- `StateChanged`
- `PositionChanged`
- `DurationChanged`
- `Failed`

The track controller exposes:

- `TracksChanged`
- `DelayChanged`

The navigation controller exposes:

- `NavigationChanged`

The diagnostics controller exposes:

- `DiagnosticsChanged`

### Threading

Playback, track, navigation and diagnostics events do **not** have UI-thread affinity.

A UI integration layer must marshal event handling to its dispatcher before touching WinUI controls or observable UI state.

## Errors

Synchronous operation failures throw `PlaybackException` with a backend-neutral `PlaybackErrorCode`.

Errors that occur asynchronously after a play request are surfaced through `Failed` and transition the engine to `PlaybackState.Failed`.

WinUI surface bootstrap failures are reported separately through `PlaybackView.InitializationFailed`.

## Disposal

`IPlaybackEngine.DisposeAsync` is idempotent.

`PlaybackView.DisposeAsync` disposes the current surface-scoped engine and detaches its surface lifecycle hooks.
