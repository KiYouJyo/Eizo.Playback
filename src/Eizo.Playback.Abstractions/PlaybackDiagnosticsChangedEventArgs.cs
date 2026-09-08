namespace Eizo.Playback;

public sealed class PlaybackDiagnosticsChangedEventArgs : EventArgs
{
    public PlaybackDiagnosticsChangedEventArgs(
        PlaybackDiagnosticsSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public PlaybackDiagnosticsSnapshot Snapshot { get; }
}
