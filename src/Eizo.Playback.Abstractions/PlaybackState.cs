namespace Eizo.Playback;

public enum PlaybackState
{
    Idle = 0,
    Opening,
    Buffering,
    Playing,
    Paused,
    Seeking,
    Stopped,
    Ended,
    Failed
}
