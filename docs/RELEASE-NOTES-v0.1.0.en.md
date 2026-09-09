# Eizo.Playback v0.1.0 — First public playback-core release

- Introduces a playback abstraction independent from the Eizo UI for open, play/pause/stop, seek, volume, playback rate, state, and failure events.
- Ships a LibVLC-based Windows backend while keeping LibVLCSharp types isolated behind the adapter layer.
- Provides a WinUI 3 `PlaybackView` with D3D11 swap-chain video output.
- Supports audio, video, and subtitle track discovery/selection, external subtitles, A/V subtitle delay, titles, and chapter navigation.
- Exposes playback capability/runtime diagnostics while avoiding disclosure of sensitive local input paths.
- Publishes four NuGet packages: `Eizo.Playback.Abstractions`, `Eizo.Playback.Core`, `Eizo.Playback.LibVLC`, and `Eizo.Playback.LibVLC.WinUI`.
- Includes a SHA-256 manifest; CI validates Release builds, tests, packaging, and package contents.

v0.1.0 is the first stable integration baseline for Eizo.Playback. Eizo v0.2.1 pins this version.