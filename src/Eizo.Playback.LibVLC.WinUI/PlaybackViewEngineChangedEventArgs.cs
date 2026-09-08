namespace Eizo.Playback.WinUI;

public sealed class PlaybackViewEngineChangedEventArgs : EventArgs
{
    public PlaybackViewEngineChangedEventArgs(
        IPlaybackEngine? previousEngine,
        IPlaybackEngine? currentEngine)
    {
        PreviousEngine = previousEngine;
        CurrentEngine = currentEngine;
    }

    public IPlaybackEngine? PreviousEngine { get; }

    public IPlaybackEngine? CurrentEngine { get; }
}
