using Eizo.Playback;

namespace Eizo.Playback.Core;

public sealed class PlaybackStateMachine
{
    private int _state = (int)PlaybackState.Idle;

    public PlaybackState State =>
        (PlaybackState)Volatile.Read(ref _state);

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public void SetState(PlaybackState nextState)
    {
        var previousState = (PlaybackState)Interlocked.Exchange(
            ref _state,
            (int)nextState);

        if (previousState == nextState)
        {
            return;
        }

        StateChanged?.Invoke(
            this,
            new PlaybackStateChangedEventArgs(previousState, nextState));
    }
}
