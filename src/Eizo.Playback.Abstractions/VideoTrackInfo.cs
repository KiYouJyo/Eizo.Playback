namespace Eizo.Playback;

public sealed record VideoTrackInfo(
    int Id,
    string? Name,
    string? Language,
    string? Codec,
    int? Bitrate,
    int? Width,
    int? Height,
    double? FrameRate,
    bool IsSelected);
