using Eizo.Playback.Core;

namespace Eizo.Playback.Core.Tests;

public sealed class PlaybackStateMachineTests
{
    [Fact]
    public void StartsInIdleState()
    {
        var machine = new PlaybackStateMachine();
        Assert.Equal(PlaybackState.Idle, machine.State);
    }

    [Fact]
    public void SetStateRaisesOneChangeEvent()
    {
        var machine = new PlaybackStateMachine();
        PlaybackStateChangedEventArgs? observed = null;

        machine.StateChanged += (_, args) => observed = args;
        machine.SetState(PlaybackState.Opening);

        Assert.NotNull(observed);
        Assert.Equal(PlaybackState.Idle, observed.PreviousState);
        Assert.Equal(PlaybackState.Opening, observed.CurrentState);
    }

    [Fact]
    public void SettingSameStateDoesNotRaiseEvent()
    {
        var machine = new PlaybackStateMachine();
        var eventCount = 0;

        machine.StateChanged += (_, _) => eventCount++;
        machine.SetState(PlaybackState.Idle);

        Assert.Equal(0, eventCount);
    }
}
