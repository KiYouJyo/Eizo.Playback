namespace Eizo.Playback;

public sealed class PlaybackTracksChangedEventArgs : EventArgs
{
    public PlaybackTracksChangedEventArgs(PlaybackTrackKind kind)
    {
        Kind = kind;
    }

    public PlaybackTrackKind Kind { get; }
}
