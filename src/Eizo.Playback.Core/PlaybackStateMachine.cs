namespace Eizo.Playback.Core;

public sealed class PlaybackStateMachine
{
    public PlaybackState State { get; private set; } = PlaybackState.Idle;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public void SetState(PlaybackState nextState)
    {
        if (State == nextState)
        {
            return;
        }

        var previousState = State;
        State = nextState;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previousState, nextState));
    }
}
