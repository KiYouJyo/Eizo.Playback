namespace Eizo.Playback;

public interface IPlaybackEngine : IAsyncDisposable
{
    PlaybackState State { get; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    double Volume { get; set; }

    double PlaybackRate { get; set; }

    IPlaybackTrackController Tracks { get; }

    IPlaybackNavigationController Navigation { get; }

    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    event EventHandler<PlaybackDurationChangedEventArgs>? DurationChanged;

    event EventHandler<PlaybackFailedEventArgs>? Failed;

    ValueTask OpenAsync(
        PlaybackSource source,
        CancellationToken cancellationToken = default);

    ValueTask PlayAsync(CancellationToken cancellationToken = default);

    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask SeekAsync(
        TimeSpan position,
        CancellationToken cancellationToken = default);
}
