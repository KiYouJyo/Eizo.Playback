# Playback contract

This document defines the application-facing behavior of `IPlaybackEngine`.

## Backend isolation

The public engine contract must never expose:

- `LibVLC`
- `MediaPlayer`
- `Media`
- `VLCState`
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

If the surface unloads and is later recreated, `EngineChanged` may provide a new engine instance.

The Eizo application must not cache a surface-scoped engine indefinitely across view destruction.

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

Values outside `0.0 .. 1.0`, NaN and infinity are rejected.

The LibVLC adapter maps this range to LibVLC's integer percentage volume.

## Playback rate

The public playback rate is a positive finite multiplier:

```text
1.0 = normal speed
0.5 = half speed
2.0 = double speed
```

Backend rejection is surfaced as a `PlaybackException`.

## Events

The engine contract exposes:

- `StateChanged`
- `PositionChanged`
- `DurationChanged`
- `Failed`

### Threading

Playback events do **not** have UI-thread affinity.

A UI integration layer must marshal event handling to its dispatcher before touching WinUI controls or observable UI state.

## Errors

Synchronous operation failures throw `PlaybackException` with a backend-neutral `PlaybackErrorCode`.

Errors that occur asynchronously after a play request are surfaced through `Failed` and transition the engine to `PlaybackState.Failed`.

WinUI surface bootstrap failures are reported separately through `PlaybackView.InitializationFailed`.

## Disposal

`IPlaybackEngine.DisposeAsync` is idempotent.

`PlaybackView.DisposeAsync` disposes the current surface-scoped engine and detaches its surface lifecycle hooks.
