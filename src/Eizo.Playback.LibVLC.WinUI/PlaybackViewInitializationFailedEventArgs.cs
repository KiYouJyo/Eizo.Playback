namespace Eizo.Playback.WinUI;

public sealed class PlaybackViewInitializationFailedEventArgs : EventArgs
{
    public PlaybackViewInitializationFailedEventArgs(Exception error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public Exception Error { get; }
}
