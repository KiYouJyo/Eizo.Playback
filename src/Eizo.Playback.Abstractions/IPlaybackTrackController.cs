namespace Eizo.Playback;

public interface IPlaybackTrackController
{
    IReadOnlyList<AudioTrackInfo> AudioTracks { get; }

    IReadOnlyList<VideoTrackInfo> VideoTracks { get; }

    IReadOnlyList<SubtitleTrackInfo> SubtitleTracks { get; }

    int? SelectedAudioTrackId { get; }

    int? SelectedVideoTrackId { get; }

    int? SelectedSubtitleTrackId { get; }

    TimeSpan AudioDelay { get; }

    TimeSpan SubtitleDelay { get; }

    event EventHandler<PlaybackTracksChangedEventArgs>? TracksChanged;

    event EventHandler<PlaybackDelayChangedEventArgs>? DelayChanged;

    ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    ValueTask SelectAudioTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default);

    ValueTask SelectVideoTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default);

    ValueTask SelectSubtitleTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default);

    ValueTask AddExternalSubtitleAsync(
        Uri source,
        bool select = true,
        CancellationToken cancellationToken = default);

    ValueTask SetAudioDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default);

    ValueTask SetSubtitleDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default);
}
