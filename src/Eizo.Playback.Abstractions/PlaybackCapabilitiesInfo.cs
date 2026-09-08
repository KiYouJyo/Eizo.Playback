namespace Eizo.Playback;

public sealed record PlaybackCapabilitiesInfo(
    bool CanSeek,
    bool CanPause,
    uint VideoOutputCount,
    double? PlaybackFps,
    double BufferingPercent,
    bool IsProgramScrambled);
