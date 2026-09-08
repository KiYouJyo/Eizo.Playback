namespace Eizo.Playback;

public sealed record PlaybackDiagnosticsSnapshot(
    DateTimeOffset CapturedAt,
    PlaybackBackendInfo Backend,
    PlaybackInputKind InputKind,
    string? InputScheme,
    PlaybackCapabilitiesInfo Capabilities,
    PlaybackMediaStatistics? Statistics,
    AudioTrackInfo? SelectedAudioTrack,
    VideoTrackInfo? SelectedVideoTrack,
    SubtitleTrackInfo? SelectedSubtitleTrack);
