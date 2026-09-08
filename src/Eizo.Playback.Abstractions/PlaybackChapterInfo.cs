namespace Eizo.Playback;

public sealed record PlaybackChapterInfo(
    int Index,
    int? TitleIndex,
    string? Name,
    TimeSpan? Start,
    TimeSpan? Duration,
    bool IsSelected)
{
    public TimeSpan? End =>
        Start is TimeSpan start && Duration is TimeSpan duration
            ? start + duration
            : null;
}
