# Track management

## Entry point

Track control is exposed from the playback engine:

```csharp
IPlaybackTrackController tracks = engine.Tracks;
```

The Eizo application does not need to reference LibVLCSharp track types.

## Available snapshots

The controller exposes:

- `AudioTracks`
- `VideoTracks`
- `SubtitleTracks`
- selected IDs for each category
- `AudioDelay`
- `SubtitleDelay`

Track objects are immutable records.

## Track metadata

Audio tracks may expose:

- name
- language
- codec
- bitrate
- channels
- sample rate

Video tracks may expose:

- name
- language
- codec
- bitrate
- width / height
- frame rate

Subtitle tracks may expose:

- name
- language
- codec
- encoding

Metadata fields are nullable because containers and backends do not always provide every value.

## Refresh behavior

LibVLC's elementary-stream descriptions become authoritative after the input is active.

The backend refreshes automatically when LibVLC reports:

- playback started
- elementary stream added
- elementary stream removed
- elementary stream selected

The application may call `RefreshAsync` at any time when it needs a fresh snapshot.

## Selection

Use nullable IDs:

```csharp
await engine.Tracks.SelectAudioTrackAsync(audioId);
await engine.Tracks.SelectSubtitleTrackAsync(subtitleId);

// Disable subtitle rendering:
await engine.Tracks.SelectSubtitleTrackAsync(null);
```

Negative LibVLC pseudo-track IDs are deliberately hidden from the public contract.

## External subtitles

```csharp
await engine.Tracks.AddExternalSubtitleAsync(
    new Uri(subtitlePath),
    select: true);
```

Local files are validated before the backend call.

Remote absolute URIs are also accepted.

## Delay controls

```csharp
await engine.Tracks.SetAudioDelayAsync(
    TimeSpan.FromMilliseconds(150));

await engine.Tracks.SetSubtitleDelayAsync(
    TimeSpan.FromMilliseconds(-250));
```

The Eizo contract uses `TimeSpan`; the LibVLC adapter converts it to LibVLC's microsecond representation.

## UI threading

`TracksChanged` and `DelayChanged` have no WinUI thread affinity.

Dispatch to the UI thread before updating bound collections or controls.
