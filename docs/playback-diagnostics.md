# Playback diagnostics

## Entry point

```csharp
IPlaybackDiagnosticsController diagnostics = engine.Diagnostics;
```

Read the latest cached snapshot:

```csharp
PlaybackDiagnosticsSnapshot snapshot = diagnostics.Current;
```

Request an authoritative refresh:

```csharp
snapshot = await diagnostics.RefreshAsync();
```

## Backend information

`snapshot.Backend` includes:

- backend name
- LibVLC version
- LibVLC changeset
- LibVLC compiler
- LibVLCSharp wrapper assembly version
- whether hardware decoding was requested
- whether active hardware decoding was confirmed

LibVLC 3 does not provide a reliable public cross-platform API that confirms the active video decoder path.

Therefore the LibVLC adapter currently reports:

```text
HardwareDecodingRequested = true/false
HardwareDecodingActive    = null
```

This is intentional. The adapter must not infer hardware decoding from GPU activity or configuration alone.

## Input privacy

Diagnostics classify the input as:

- `None`
- `LocalFile`
- `Network`
- `OtherUri`

and expose only the URI scheme, for example:

```text
file
https
rtsp
webdav
```

The full URI/path is not stored in the diagnostic snapshot.

This prevents credentials and signed query parameters from being propagated into diagnostic screens or logs.

## Capabilities

`snapshot.Capabilities` exposes:

- `CanSeek`
- `CanPause`
- `VideoOutputCount`
- `PlaybackFps`
- `BufferingPercent`
- `IsProgramScrambled`

FPS is null when LibVLC does not report a meaningful positive value.

## Media statistics

When a current media descriptor is available, `snapshot.Statistics` contains backend-normalized counters:

- bytes read
- demux bytes read
- corrupted/discontinuity counts
- decoded audio/video counts
- displayed/lost pictures
- played/lost audio buffers
- sent packets/bytes

The diagnostic layer intentionally avoids assigning its own units to LibVLC raw bitrate fields in Stage 5. Stable byte/count metrics are exposed first.

## Selected tracks

The snapshot includes the currently selected:

- audio track
- video track
- subtitle track

using the same backend-neutral track records exposed through `engine.Tracks`.

## Update behavior

Automatic diagnostics updates are observational and best-effort.

Native callbacks never wait for a busy diagnostics refresh lock. If an automatic refresh is skipped, the UI can request `RefreshAsync()` later.

`DiagnosticsChanged` has no WinUI thread affinity.
