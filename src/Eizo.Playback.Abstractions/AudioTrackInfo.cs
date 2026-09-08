namespace Eizo.Playback;

public sealed record AudioTrackInfo(
    int Id,
    string? Name,
    string? Language,
    string? Codec,
    int? Bitrate,
    int? Channels,
    int? SampleRate,
    bool IsSelected);
