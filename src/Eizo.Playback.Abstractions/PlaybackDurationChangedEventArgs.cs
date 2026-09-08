namespace Eizo.Playback;

public sealed class PlaybackDurationChangedEventArgs : EventArgs
{
    public PlaybackDurationChangedEventArgs(TimeSpan duration)
    {
        Duration = duration;
    }

    public TimeSpan Duration { get; }
}
