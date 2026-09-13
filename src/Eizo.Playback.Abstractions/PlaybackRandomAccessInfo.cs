namespace Eizo.Playback;

public sealed record PlaybackRandomAccessInfo(
    long? Length,
    bool CanSeek = true);
