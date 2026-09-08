namespace Eizo.Playback;

public interface IPlaybackDiagnosticsController
{
    PlaybackDiagnosticsSnapshot Current { get; }

    event EventHandler<PlaybackDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    ValueTask<PlaybackDiagnosticsSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default);
}
