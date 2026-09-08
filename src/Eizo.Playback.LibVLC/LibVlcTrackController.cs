using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

internal sealed class LibVlcTrackController : IPlaybackTrackController, IAsyncDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _snapshotGate = new();

    private IReadOnlyList<AudioTrackInfo> _audioTracks = Array.Empty<AudioTrackInfo>();
    private IReadOnlyList<VideoTrackInfo> _videoTracks = Array.Empty<VideoTrackInfo>();
    private IReadOnlyList<SubtitleTrackInfo> _subtitleTracks = Array.Empty<SubtitleTrackInfo>();

    private int? _selectedAudioTrackId;
    private int? _selectedVideoTrackId;
    private int? _selectedSubtitleTrackId;
    private int _disposeState;

    public LibVlcTrackController(MediaPlayer mediaPlayer)
    {
        _mediaPlayer = mediaPlayer ?? throw new ArgumentNullException(nameof(mediaPlayer));

        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.ESAdded += OnElementaryStreamAdded;
        _mediaPlayer.ESDeleted += OnElementaryStreamDeleted;
        _mediaPlayer.ESSelected += OnElementaryStreamSelected;
    }

    public IReadOnlyList<AudioTrackInfo> AudioTracks
    {
        get
        {
            lock (_snapshotGate)
            {
                return _audioTracks;
            }
        }
    }

    public IReadOnlyList<VideoTrackInfo> VideoTracks
    {
        get
        {
            lock (_snapshotGate)
            {
                return _videoTracks;
            }
        }
    }

    public IReadOnlyList<SubtitleTrackInfo> SubtitleTracks
    {
        get
        {
            lock (_snapshotGate)
            {
                return _subtitleTracks;
            }
        }
    }

    public int? SelectedAudioTrackId
    {
        get
        {
            lock (_snapshotGate)
            {
                return _selectedAudioTrackId;
            }
        }
    }

    public int? SelectedVideoTrackId
    {
        get
        {
            lock (_snapshotGate)
            {
                return _selectedVideoTrackId;
            }
        }
    }

    public int? SelectedSubtitleTrackId
    {
        get
        {
            lock (_snapshotGate)
            {
                return _selectedSubtitleTrackId;
            }
        }
    }

    public TimeSpan AudioDelay
    {
        get
        {
            ThrowIfDisposed();
            return FromMicroseconds(_mediaPlayer.AudioDelay);
        }
    }

    public TimeSpan SubtitleDelay
    {
        get
        {
            ThrowIfDisposed();
            return FromMicroseconds(_mediaPlayer.SpuDelay);
        }
    }

    public event EventHandler<PlaybackTracksChangedEventArgs>? TracksChanged;

    public event EventHandler<PlaybackDelayChangedEventArgs>? DelayChanged;

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(PlaybackTrackKind.All);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask SelectAudioTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default) =>
        SelectTrackAsync(
            PlaybackTrackKind.Audio,
            trackId,
            static (player, id) => player.SetAudioTrack(id),
            cancellationToken);

    public ValueTask SelectVideoTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default) =>
        SelectTrackAsync(
            PlaybackTrackKind.Video,
            trackId,
            static (player, id) => player.SetVideoTrack(id),
            cancellationToken);

    public ValueTask SelectSubtitleTrackAsync(
        int? trackId,
        CancellationToken cancellationToken = default) =>
        SelectTrackAsync(
            PlaybackTrackKind.Subtitle,
            trackId,
            static (player, id) => player.SetSpu(id),
            cancellationToken);

    public async ValueTask AddExternalSubtitleAsync(
        Uri source,
        bool select = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfDisposed();

        if (!source.IsAbsoluteUri)
        {
            throw new PlaybackException(
                PlaybackErrorCode.InvalidSource,
                "External subtitle URI must be absolute.");
        }

        if (source.IsFile && !File.Exists(source.LocalPath))
        {
            throw new PlaybackException(
                PlaybackErrorCode.FileNotFound,
                $"External subtitle file was not found: {source.LocalPath}");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (!_mediaPlayer.AddSlave(
                MediaSlaveType.Subtitle,
                source.AbsoluteUri,
                select))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.ExternalSubtitleFailed,
                    $"LibVLC failed to add external subtitle '{source}'.");
            }

            TryRefresh(PlaybackTrackKind.Subtitle);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask SetAudioDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        SetDelayAsync(
            PlaybackDelayKind.Audio,
            delay,
            static (player, microseconds) => player.SetAudioDelay(microseconds),
            cancellationToken);

    public ValueTask SetSubtitleDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        SetDelayAsync(
            PlaybackDelayKind.Subtitle,
            delay,
            static (player, microseconds) => player.SetSpuDelay(microseconds),
            cancellationToken);

    public void Reset()
    {
        ThrowIfDisposed();

        lock (_snapshotGate)
        {
            _audioTracks = Array.Empty<AudioTrackInfo>();
            _videoTracks = Array.Empty<VideoTrackInfo>();
            _subtitleTracks = Array.Empty<SubtitleTrackInfo>();
            _selectedAudioTrackId = null;
            _selectedVideoTrackId = null;
            _selectedSubtitleTrackId = null;
        }

        TracksChanged?.Invoke(
            this,
            new PlaybackTracksChangedEventArgs(PlaybackTrackKind.All));
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
            _mediaPlayer.ESAdded -= OnElementaryStreamAdded;
            _mediaPlayer.ESDeleted -= OnElementaryStreamDeleted;
            _mediaPlayer.ESSelected -= OnElementaryStreamSelected;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async ValueTask SelectTrackAsync(
        PlaybackTrackKind kind,
        int? trackId,
        Func<MediaPlayer, int, bool> select,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            RefreshCore(kind, raiseEvent: false);

            if (trackId is int id && !ContainsTrack(kind, id))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.TrackNotFound,
                    $"{kind} track {id} does not exist.");
            }

            if (!select(_mediaPlayer, trackId ?? -1))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.TrackSelectionFailed,
                    $"LibVLC failed to select {kind} track {trackId?.ToString() ?? "disabled"}.");
            }

            TryRefresh(kind);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask SetDelayAsync(
        PlaybackDelayKind kind,
        TimeSpan delay,
        Func<MediaPlayer, long, bool> set,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            var microseconds = delay.Ticks / 10;

            if (!set(_mediaPlayer, microseconds))
            {
                throw new PlaybackException(
                    PlaybackErrorCode.DelayUpdateFailed,
                    $"LibVLC failed to update {kind} delay.");
            }

            DelayChanged?.Invoke(
                this,
                new PlaybackDelayChangedEventArgs(kind, delay));
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool ContainsTrack(PlaybackTrackKind kind, int trackId)
    {
        lock (_snapshotGate)
        {
            return kind switch
            {
                PlaybackTrackKind.Audio => _audioTracks.Any(track => track.Id == trackId),
                PlaybackTrackKind.Video => _videoTracks.Any(track => track.Id == trackId),
                PlaybackTrackKind.Subtitle => _subtitleTracks.Any(track => track.Id == trackId),
                _ => false
            };
        }
    }

    private void RefreshCore(
        PlaybackTrackKind changedKind,
        bool raiseEvent = true)
    {
        MediaTrack[] mediaTracks;

        using (var media = _mediaPlayer.Media)
        {
            mediaTracks = media?.Tracks ?? Array.Empty<MediaTrack>();
        }

        var metadataById = mediaTracks
            .GroupBy(static track => track.Id)
            .ToDictionary(static group => group.Key, static group => group.First());

        var selectedAudio = NormalizeTrackId(_mediaPlayer.AudioTrack);
        var selectedVideo = NormalizeTrackId(_mediaPlayer.VideoTrack);
        var selectedSubtitle = NormalizeTrackId(_mediaPlayer.Spu);

        var audioTracks = _mediaPlayer.AudioTrackDescription
            .Where(static description => description.Id >= 0)
            .Select(description =>
                MapAudioTrack(
                    description.Id,
                    description.Name,
                    selectedAudio,
                    metadataById))
            .ToArray();

        var videoTracks = _mediaPlayer.VideoTrackDescription
            .Where(static description => description.Id >= 0)
            .Select(description =>
                MapVideoTrack(
                    description.Id,
                    description.Name,
                    selectedVideo,
                    metadataById))
            .ToArray();

        var subtitleTracks = _mediaPlayer.SpuDescription
            .Where(static description => description.Id >= 0)
            .Select(description =>
                MapSubtitleTrack(
                    description.Id,
                    description.Name,
                    selectedSubtitle,
                    metadataById))
            .ToArray();

        lock (_snapshotGate)
        {
            _audioTracks = Array.AsReadOnly(audioTracks);
            _videoTracks = Array.AsReadOnly(videoTracks);
            _subtitleTracks = Array.AsReadOnly(subtitleTracks);
            _selectedAudioTrackId = selectedAudio;
            _selectedVideoTrackId = selectedVideo;
            _selectedSubtitleTrackId = selectedSubtitle;
        }

        if (raiseEvent)
        {
            TracksChanged?.Invoke(
                this,
                new PlaybackTracksChangedEventArgs(changedKind));
        }
    }

    private static AudioTrackInfo MapAudioTrack(
        int id,
        string? name,
        int? selectedId,
        IReadOnlyDictionary<int, MediaTrack> metadataById)
    {
        if (!metadataById.TryGetValue(id, out var metadata)
            || metadata.TrackType != TrackType.Audio)
        {
            return new AudioTrackInfo(
                id,
                name,
                null,
                null,
                null,
                null,
                null,
                selectedId == id);
        }

        return new AudioTrackInfo(
            id,
            PreferName(name, metadata.Description),
            Normalize(metadata.Language),
            LibVlcCodecFormatter.FromFourCc(metadata.Codec),
            ToNullableInt(metadata.Bitrate),
            ToNullableInt(metadata.Data.Audio.Channels),
            ToNullableInt(metadata.Data.Audio.Rate),
            selectedId == id);
    }

    private static VideoTrackInfo MapVideoTrack(
        int id,
        string? name,
        int? selectedId,
        IReadOnlyDictionary<int, MediaTrack> metadataById)
    {
        if (!metadataById.TryGetValue(id, out var metadata)
            || metadata.TrackType != TrackType.Video)
        {
            return new VideoTrackInfo(
                id,
                name,
                null,
                null,
                null,
                null,
                null,
                null,
                selectedId == id);
        }

        var video = metadata.Data.Video;
        var frameRate = video.FrameRateDen == 0
            ? null
            : (double?)video.FrameRateNum / video.FrameRateDen;

        return new VideoTrackInfo(
            id,
            PreferName(name, metadata.Description),
            Normalize(metadata.Language),
            LibVlcCodecFormatter.FromFourCc(metadata.Codec),
            ToNullableInt(metadata.Bitrate),
            ToNullableInt(video.Width),
            ToNullableInt(video.Height),
            frameRate,
            selectedId == id);
    }

    private static SubtitleTrackInfo MapSubtitleTrack(
        int id,
        string? name,
        int? selectedId,
        IReadOnlyDictionary<int, MediaTrack> metadataById)
    {
        if (!metadataById.TryGetValue(id, out var metadata)
            || metadata.TrackType != TrackType.Text)
        {
            return new SubtitleTrackInfo(
                id,
                name,
                null,
                null,
                null,
                selectedId == id);
        }

        return new SubtitleTrackInfo(
            id,
            PreferName(name, metadata.Description),
            Normalize(metadata.Language),
            LibVlcCodecFormatter.FromFourCc(metadata.Codec),
            Normalize(metadata.Data.Subtitle.Encoding),
            selectedId == id);
    }

    private static string? PreferName(string? descriptionName, string? mediaDescription) =>
        Normalize(descriptionName) ?? Normalize(mediaDescription);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static int? NormalizeTrackId(int id) =>
        id < 0 ? null : id;

    private static int? ToNullableInt(uint value) =>
        value == 0
            ? null
            : value > int.MaxValue
                ? int.MaxValue
                : (int)value;

    private static TimeSpan FromMicroseconds(long microseconds)
    {
        if (microseconds > TimeSpan.MaxValue.Ticks / 10)
        {
            return TimeSpan.MaxValue;
        }

        if (microseconds < TimeSpan.MinValue.Ticks / 10)
        {
            return TimeSpan.MinValue;
        }

        return TimeSpan.FromTicks(microseconds * 10);
    }

    private void OnPlaying(object? sender, EventArgs eventArgs) =>
        TryRefresh(PlaybackTrackKind.All);

    private void OnElementaryStreamAdded(
        object? sender,
        MediaPlayerESAddedEventArgs eventArgs) =>
        TryRefresh(ToPlaybackTrackKind(eventArgs.Type));

    private void OnElementaryStreamDeleted(
        object? sender,
        MediaPlayerESDeletedEventArgs eventArgs) =>
        TryRefresh(ToPlaybackTrackKind(eventArgs.Type));

    private void OnElementaryStreamSelected(
        object? sender,
        MediaPlayerESSelectedEventArgs eventArgs) =>
        TryRefresh(ToPlaybackTrackKind(eventArgs.Type));

    private void TryRefresh(PlaybackTrackKind kind)
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
            // Native callbacks must never be allowed to escape into LibVLC.
            // The next explicit RefreshAsync call can surface current data.
        }
    }

    private static PlaybackTrackKind ToPlaybackTrackKind(TrackType type) =>
        type switch
        {
            TrackType.Audio => PlaybackTrackKind.Audio,
            TrackType.Video => PlaybackTrackKind.Video,
            TrackType.Text => PlaybackTrackKind.Subtitle,
            _ => PlaybackTrackKind.All
        };

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(LibVlcTrackController));
        }
    }
}
