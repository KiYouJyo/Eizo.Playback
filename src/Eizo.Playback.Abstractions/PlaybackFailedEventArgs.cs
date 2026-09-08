namespace Eizo.Playback;

public sealed class PlaybackFailedEventArgs : EventArgs
{
    public PlaybackFailedEventArgs(PlaybackException error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public PlaybackException Error { get; }
}
