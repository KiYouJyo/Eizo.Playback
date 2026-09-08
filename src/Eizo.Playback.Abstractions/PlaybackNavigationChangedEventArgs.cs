namespace Eizo.Playback;

public sealed class PlaybackNavigationChangedEventArgs : EventArgs
{
    public PlaybackNavigationChangedEventArgs(
        PlaybackNavigationChangeKind kind)
    {
        Kind = kind;
    }

    public PlaybackNavigationChangeKind Kind { get; }
}
