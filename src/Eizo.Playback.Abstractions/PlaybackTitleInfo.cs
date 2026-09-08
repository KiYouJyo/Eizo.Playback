namespace Eizo.Playback;

public sealed record PlaybackTitleInfo(
    int Index,
    string? Name,
    int ChapterCount,
    bool IsSelected);
