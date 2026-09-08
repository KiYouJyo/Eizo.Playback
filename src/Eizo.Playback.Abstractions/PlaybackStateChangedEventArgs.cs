namespace Eizo.Playback;

public sealed class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackStateChangedEventArgs(PlaybackState previousState, PlaybackState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    public PlaybackState PreviousState { get; }
    public PlaybackState CurrentState { get; }
}
