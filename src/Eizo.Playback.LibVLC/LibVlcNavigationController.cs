using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;

namespace Eizo.Playback.Backends.LibVLC;

internal sealed class LibVlcNavigationController : IPlaybackNavigationController, IAsyncDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _snapshotGate = new();

    private IReadOnlyList<PlaybackTitleInfo> _titles = Array.Empty<PlaybackTitleInfo>();
    private IReadOnlyList<PlaybackChapterInfo> _chapters = Array.Empty<PlaybackChapterInfo>();
    private int? _selectedTitleIndex;
    private int? _selectedChapterIndex;
    private int _disposeState;

    public LibVlcNavigationController(MediaPlayer mediaPlayer)
    {
        _mediaPlayer = mediaPlayer ?? throw new ArgumentNullException(nameof(mediaPlayer));

        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.TitleChanged += OnTitleChanged;
        _mediaPlayer.ChapterChanged += OnChapterChanged;
    }

    public IReadOnlyList<PlaybackTitleInfo> Titles
    {
        get
        {
            lock (_snapshotGate)
            {
                return _titles;
            }
        }
    }

    public IReadOnlyList<PlaybackChapterInfo> Chapters
    {
        get
        {
            lock (_snapshotGate)
            {
                return _chapters;
            }
        }
    }

    public int? SelectedTitleIndex
    {
        get
        {
            lock (_snapshotGate)
            {
                return _selectedTitleIndex;
            }
        }
    }

    public int? SelectedChapterIndex
    {
        get
        {
            lock (_snapshotGate)
            {
                return _selectedChapterIndex;
            }
        }
    }

    public event EventHandler<PlaybackNavigationChangedEventArgs>? NavigationChanged;

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackNavigationChangeKind.All);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SelectTitleAsync(
        int titleIndex,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackNavigationChangeKind.All, raiseEvent: false);

            if (!ContainsTitle(titleIndex))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.TitleNotFound,
                    $"Title {titleIndex} does not exist.");
            }

            _mediaPlayer.Title = titleIndex;
            TryRefresh(PlaybackNavigationChangeKind.All);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask SelectChapterAsync(
        int chapterIndex,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackNavigationChangeKind.Chapters, raiseEvent: false);

            if (!ContainsChapter(chapterIndex))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.ChapterNotFound,
                    $"Chapter {chapterIndex} does not exist in the current title.");
            }

            _mediaPlayer.Chapter = chapterIndex;
            TryRefresh(PlaybackNavigationChangeKind.Selection);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> NextChapterAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackNavigationChangeKind.Chapters, raiseEvent: false);

            PlaybackChapterInfo[] chapters;
            int? selected;

            lock (_snapshotGate)
            {
                chapters = _chapters.ToArray();
                selected = _selectedChapterIndex;
            }

            if (chapters.Length == 0)
            {
                return false;
            }

            var target = selected is null
                ? chapters[0]
                : FindAdjacentChapter(chapters, selected.Value, offset: 1);

            if (target is null)
            {
                return false;
            }

            _mediaPlayer.Chapter = target.Index;
            TryRefresh(PlaybackNavigationChangeKind.Selection);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> PreviousChapterAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackNavigationChangeKind.Chapters, raiseEvent: false);

            PlaybackChapterInfo[] chapters;
            int? selected;

            lock (_snapshotGate)
            {
                chapters = _chapters.ToArray();
                selected = _selectedChapterIndex;
            }

            if (chapters.Length == 0 || selected is null)
            {
                return false;
            }

            var target = FindAdjacentChapter(chapters, selected.Value, offset: -1);

            if (target is null)
            {
                return false;
            }

            _mediaPlayer.Chapter = target.Index;
            TryRefresh(PlaybackNavigationChangeKind.Selection);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();

        lock (_snapshotGate)
        {
            _titles = Array.Empty<PlaybackTitleInfo>();
            _chapters = Array.Empty<PlaybackChapterInfo>();
            _selectedTitleIndex = null;
            _selectedChapterIndex = null;
        }

        NavigationChanged?.Invoke(
            this,
            new PlaybackNavigationChangedEventArgs(
                PlaybackNavigationChangeKind.All));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            _mediaPlayer.Playing -= OnPlaying;
            _mediaPlayer.TitleChanged -= OnTitleChanged;
            _mediaPlayer.ChapterChanged -= OnChapterChanged;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private bool ContainsTitle(int titleIndex)
    {
        lock (_snapshotGate)
        {
            return _titles.Any(title => title.Index == titleIndex);
        }
    }

    private bool ContainsChapter(int chapterIndex)
    {
        lock (_snapshotGate)
        {
            return _chapters.Any(chapter => chapter.Index == chapterIndex);
        }
    }

    private void RefreshCore(
        PlaybackNavigationChangeKind kind,
        bool raiseEvent = true)
    {
        var selectedTitle = NormalizeIndex(_mediaPlayer.Title);
        var selectedChapter = NormalizeIndex(_mediaPlayer.Chapter);

        var titles = ReadTitles(selectedTitle);
        var chapters = ReadChapters(selectedTitle, selectedChapter);

        lock (_snapshotGate)
        {
            _titles = Array.AsReadOnly(titles);
            _chapters = Array.AsReadOnly(chapters);
            _selectedTitleIndex = selectedTitle;
            _selectedChapterIndex = selectedChapter;
        }

        if (raiseEvent)
        {
            NavigationChanged?.Invoke(
                this,
                new PlaybackNavigationChangedEventArgs(kind));
        }
    }

    private PlaybackTitleInfo[] ReadTitles(int? selectedTitle)
    {
        var titleCount = _mediaPlayer.TitleCount;

        if (titleCount <= 0)
        {
            return Array.Empty<PlaybackTitleInfo>();
        }

        TrackDescription[] descriptions;

        try
        {
            descriptions = _mediaPlayer.TitleDescription;
        }
        catch
        {
            descriptions = Array.Empty<TrackDescription>();
        }

        var descriptionsById = descriptions
            .GroupBy(static description => description.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        return Enumerable.Range(0, titleCount)
            .Select(index =>
            {
                var name = descriptionsById.TryGetValue(index, out var description)
                    ? NormalizeText(description.Name)
                    : null;

                var chapterCount = _mediaPlayer.ChapterCountForTitle(index);

                return new PlaybackTitleInfo(
                    index,
                    name,
                    Math.Max(chapterCount, 0),
                    selectedTitle == index);
            })
            .ToArray();
    }

    private PlaybackChapterInfo[] ReadChapters(
        int? selectedTitle,
        int? selectedChapter)
    {
        var titleIndex = selectedTitle ?? -1;

        try
        {
            var fullDescriptions = _mediaPlayer.FullChapterDescriptions(titleIndex);

            if (fullDescriptions.Length > 0)
            {
                return fullDescriptions
                    .Select((description, index) =>
                        new PlaybackChapterInfo(
                            index,
                            selectedTitle,
                            NormalizeText(description.Name),
                            FromMilliseconds(description.TimeOffset),
                            FromPositiveMilliseconds(description.Duration),
                            selectedChapter == index))
                    .ToArray();
            }
        }
        catch
        {
        }

        try
        {
            return _mediaPlayer.ChapterDescription(titleIndex)
                .Where(static description => description.Id >= 0)
                .Select(description =>
                    new PlaybackChapterInfo(
                        description.Id,
                        selectedTitle,
                        NormalizeText(description.Name),
                        null,
                        null,
                        selectedChapter == description.Id))
                .ToArray();
        }
        catch
        {
            return Array.Empty<PlaybackChapterInfo>();
        }
    }

    private static PlaybackChapterInfo? FindAdjacentChapter(
        IReadOnlyList<PlaybackChapterInfo> chapters,
        int currentChapterIndex,
        int offset)
    {
        var currentPosition = -1;

        for (var index = 0; index < chapters.Count; index++)
        {
            if (chapters[index].Index == currentChapterIndex)
            {
                currentPosition = index;
                break;
            }
        }

        if (currentPosition < 0)
        {
            return null;
        }

        var targetPosition = currentPosition + offset;

        return targetPosition >= 0 && targetPosition < chapters.Count
            ? chapters[targetPosition]
            : null;
    }

    private void OnPlaying(object? sender, EventArgs eventArgs) =>
        TryRefresh(PlaybackNavigationChangeKind.All);

    private void OnTitleChanged(
        object? sender,
        MediaPlayerTitleChangedEventArgs eventArgs) =>
        TryRefresh(PlaybackNavigationChangeKind.All);

    private void OnChapterChanged(
        object? sender,
        MediaPlayerChapterChangedEventArgs eventArgs) =>
        TryRefresh(PlaybackNavigationChangeKind.Selection);

    private void TryRefresh(PlaybackNavigationChangeKind kind)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        try
        {
            RefreshCore(kind);
        }
        catch
        {
            // LibVLC callbacks must never receive exceptions from the adapter.
            // Callers can use RefreshAsync when they need an explicit snapshot.
        }
    }

    private static int? NormalizeIndex(int index) =>
        index < 0 ? null : index;

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static TimeSpan? FromMilliseconds(long milliseconds)
    {
        if (milliseconds < 0)
        {
            return null;
        }

        if (milliseconds > TimeSpan.MaxValue.TotalMilliseconds)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static TimeSpan? FromPositiveMilliseconds(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return null;
        }

        if (milliseconds > TimeSpan.MaxValue.TotalMilliseconds)
        {
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(LibVlcNavigationController));
        }
    }
}
