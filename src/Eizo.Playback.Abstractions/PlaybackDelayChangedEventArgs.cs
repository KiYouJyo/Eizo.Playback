namespace Eizo.Playback;

public sealed class PlaybackDelayChangedEventArgs : EventArgs
{
    public PlaybackDelayChangedEventArgs(
        PlaybackDelayKind kind,
        TimeSpan delay)
    {
        Kind = kind;
        Delay = delay;
    }

    public PlaybackDelayKind Kind { get; }

    public TimeSpan Delay { get; }
}
