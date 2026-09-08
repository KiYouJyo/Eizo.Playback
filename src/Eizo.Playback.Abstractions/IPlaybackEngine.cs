namespace Eizo.Playback;

public interface IPlaybackEngine : IAsyncDisposable
{
    PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    double PlaybackRate { get; set; }

    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    ValueTask OpenAsync(PlaybackSource source, CancellationToken cancellationToken = default);
    ValueTask PlayAsync(CancellationToken cancellationToken = default);
    ValueTask PauseAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}
