# Playback contract

This document defines the application-facing behavior of `IPlaybackEngine`.

## Backend isolation

The public contract must never expose:

- `LibVLC`
- `MediaPlayer`
- `Media`
- `VLCState`
- any other LibVLCSharp type

The Eizo UI is expected to depend on `Eizo.Playback.Abstractions`, not on LibVLCSharp.

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

The Stage 1 contract exposes:

- `StateChanged`
- `PositionChanged`
- `DurationChanged`
- `Failed`

### Threading

Playback events do **not** have UI-thread affinity.

A UI integration layer must marshal event handling to its dispatcher before touching WinUI controls or observable UI state.

This keeps the core playback package independent from WinUI.

## Errors

Synchronous operation failures throw `PlaybackException` with a backend-neutral `PlaybackErrorCode`.

Errors that occur asynchronously after a play request are surfaced through `Failed` and transition the engine to `PlaybackState.Failed`.

## Disposal

`DisposeAsync` is idempotent.

The LibVLC adapter owns its native `LibVLC` and `MediaPlayer` instances and releases them during disposal.
