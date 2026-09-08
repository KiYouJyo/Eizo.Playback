namespace Eizo.Playback;

public sealed record SubtitleTrackInfo(
    int Id,
    string? Name,
    string? Language,
    string? Codec,
    string? Encoding,
    bool IsSelected);
