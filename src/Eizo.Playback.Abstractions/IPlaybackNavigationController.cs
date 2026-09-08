namespace Eizo.Playback;

public interface IPlaybackNavigationController
{
    IReadOnlyList<PlaybackTitleInfo> Titles { get; }

    IReadOnlyList<PlaybackChapterInfo> Chapters { get; }

    int? SelectedTitleIndex { get; }

    int? SelectedChapterIndex { get; }

    event EventHandler<PlaybackNavigationChangedEventArgs>? NavigationChanged;

    ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    ValueTask SelectTitleAsync(
        int titleIndex,
        CancellationToken cancellationToken = default);

    ValueTask SelectChapterAsync(
        int chapterIndex,
        CancellationToken cancellationToken = default);

    ValueTask<bool> NextChapterAsync(
        CancellationToken cancellationToken = default);

    ValueTask<bool> PreviousChapterAsync(
        CancellationToken cancellationToken = default);
}
