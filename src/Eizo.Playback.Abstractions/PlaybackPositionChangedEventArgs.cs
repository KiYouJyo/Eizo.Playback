namespace Eizo.Playback;

public sealed class PlaybackPositionChangedEventArgs : EventArgs
{
    public PlaybackPositionChangedEventArgs(TimeSpan position)
    {
        Position = position;
    }

    public TimeSpan Position { get; }
}
