using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

internal sealed class LibVlcDiagnosticsController : IPlaybackDiagnosticsController, IAsyncDisposable
{
    private static readonly HashSet<string> NetworkSchemes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "http",
            "https",
            "rtsp",
            "rtmp",
            "ftp",
            "ftps",
            "sftp",
            "smb",
            "webdav",
            "webdavs"
        };

    private readonly LibVLCSharp.Shared.LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private readonly LibVlcTrackController _trackController;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _snapshotGate = new();
    private readonly PlaybackBackendInfo _backendInfo;

    private PlaybackDiagnosticsSnapshot _current;
    private PlaybackInputKind _inputKind;
    private string? _inputScheme;
    private double _bufferingPercent;
    private int _disposeState;

    public LibVlcDiagnosticsController(
        LibVLCSharp.Shared.LibVLC libVlc,
        MediaPlayer mediaPlayer,
        LibVlcTrackController trackController,
        LibVlcPlaybackOptions options)
    {
        _libVlc = libVlc ?? throw new ArgumentNullException(nameof(libVlc));
        _mediaPlayer = mediaPlayer ?? throw new ArgumentNullException(nameof(mediaPlayer));
        _trackController = trackController ?? throw new ArgumentNullException(nameof(trackController));
        ArgumentNullException.ThrowIfNull(options);

        _backendInfo = new PlaybackBackendInfo(
            Name: "LibVLC",
            Version: Normalize(_libVlc.Version),
            Changeset: Normalize(_libVlc.Changeset),
            Compiler: Normalize(_libVlc.LibVLCCompiler),
            WrapperVersion: typeof(LibVLCSharp.Shared.LibVLC).Assembly.GetName().Version,
            HardwareDecodingRequested: options.EnableHardwareDecoding,
            HardwareDecodingActive: null);

        _current = CreateSnapshot();

        _mediaPlayer.Buffering += OnBuffering;
        _mediaPlayer.Playing += OnPlaybackActivity;
        _mediaPlayer.Paused += OnPlaybackActivity;
        _mediaPlayer.Stopped += OnPlaybackActivity;
        _mediaPlayer.EndReached += OnPlaybackActivity;
        _mediaPlayer.EncounteredError += OnPlaybackActivity;
        _trackController.TracksChanged += OnTracksChanged;
    }

    public PlaybackDiagnosticsSnapshot Current
    {
        get
        {
            lock (_snapshotGate)
            {
                return _current;
            }
        }
    }

    public event EventHandler<PlaybackDiagnosticsChangedEventArgs>? DiagnosticsChanged;

    public async ValueTask<PlaybackDiagnosticsSnapshot> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            return RefreshCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SetSource(PlaybackSource? source)
    {
        ThrowIfDisposed();

        if (source is null)
        {
            _inputKind = PlaybackInputKind.None;
            _inputScheme = null;
            _bufferingPercent = 0d;
            TryRefresh();
            return;
        }

        _inputScheme = Normalize(source.Uri.Scheme)?.ToLowerInvariant();
        _inputKind = ClassifyInput(source.Uri);
        _bufferingPercent = 0d;

        TryRefresh();
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
            _mediaPlayer.Buffering -= OnBuffering;
            _mediaPlayer.Playing -= OnPlaybackActivity;
            _mediaPlayer.Paused -= OnPlaybackActivity;
            _mediaPlayer.Stopped -= OnPlaybackActivity;
            _mediaPlayer.EndReached -= OnPlaybackActivity;
            _mediaPlayer.EncounteredError -= OnPlaybackActivity;
            _trackController.TracksChanged -= OnTracksChanged;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private PlaybackDiagnosticsSnapshot RefreshCore()
    {
        var snapshot = CreateSnapshot();

        lock (_snapshotGate)
        {
            _current = snapshot;
        }

        DiagnosticsChanged?.Invoke(
            this,
            new PlaybackDiagnosticsChangedEventArgs(snapshot));

        return snapshot;
    }

    private PlaybackDiagnosticsSnapshot CreateSnapshot()
    {
        PlaybackMediaStatistics? statistics = null;

        try
        {
            using var media = _mediaPlayer.Media;

            if (media is not null)
            {
                statistics = MapStatistics(media.Statistics);
            }
        }
        catch
        {
            statistics = null;
        }

        var fps = NormalizeFps(_mediaPlayer.Fps);

        var capabilities = new PlaybackCapabilitiesInfo(
            CanSeek: _mediaPlayer.IsSeekable,
            CanPause: _mediaPlayer.CanPause,
            VideoOutputCount: _mediaPlayer.VoutCount,
            PlaybackFps: fps,
            BufferingPercent: Math.Clamp(_bufferingPercent, 0d, 100d),
            IsProgramScrambled: _mediaPlayer.ProgramScambled);

        return new PlaybackDiagnosticsSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            Backend: _backendInfo,
            InputKind: _inputKind,
            InputScheme: _inputScheme,
            Capabilities: capabilities,
            Statistics: statistics,
            SelectedAudioTrack: _trackController.AudioTracks.FirstOrDefault(static track => track.IsSelected),
            SelectedVideoTrack: _trackController.VideoTracks.FirstOrDefault(static track => track.IsSelected),
            SelectedSubtitleTrack: _trackController.SubtitleTracks.FirstOrDefault(static track => track.IsSelected));
    }

    private void TryRefresh()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        if (!_gate.Wait(0))
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _disposeState) == 0)
            {
                RefreshCore();
            }
        }
        catch
        {
            // Diagnostics are observational. Native playback callbacks must not
            // fail because a diagnostic refresh could not be produced.
        }
        finally
        {
            _gate.Release();
        }
    }

    private void OnBuffering(
        object? sender,
        MediaPlayerBufferingEventArgs eventArgs)
    {
        _bufferingPercent = Math.Clamp(eventArgs.Cache, 0f, 100f);
        TryRefresh();
    }

    private void OnPlaybackActivity(object? sender, EventArgs eventArgs)
    {
        if (_mediaPlayer.IsPlaying)
        {
            _bufferingPercent = 100d;
        }

        TryRefresh();
    }

    private void OnTracksChanged(
        object? sender,
        PlaybackTracksChangedEventArgs eventArgs) =>
        TryRefresh();

    private static PlaybackMediaStatistics MapStatistics(MediaStats statistics) =>
        new(
            ReadBytes: Math.Max(statistics.ReadBytes, 0),
            DemuxReadBytes: Math.Max(statistics.DemuxReadBytes, 0),
            DemuxCorrupted: Math.Max(statistics.DemuxCorrupted, 0),
            DemuxDiscontinuity: Math.Max(statistics.DemuxDiscontinuity, 0),
            DecodedVideo: Math.Max(statistics.DecodedVideo, 0),
            DecodedAudio: Math.Max(statistics.DecodedAudio, 0),
            DisplayedPictures: Math.Max(statistics.DisplayedPictures, 0),
            LostPictures: Math.Max(statistics.LostPictures, 0),
            PlayedAudioBuffers: Math.Max(statistics.PlayedAudioBuffers, 0),
            LostAudioBuffers: Math.Max(statistics.LostAudioBuffers, 0),
            SentPackets: Math.Max(statistics.SentPackets, 0),
            SentBytes: Math.Max(statistics.SentBytes, 0));

    private static PlaybackInputKind ClassifyInput(Uri uri)
    {
        if (uri.IsFile)
        {
            return PlaybackInputKind.LocalFile;
        }

        if (NetworkSchemes.Contains(uri.Scheme))
        {
            return PlaybackInputKind.Network;
        }

        return PlaybackInputKind.OtherUri;
    }

    private static double? NormalizeFps(float fps) =>
        float.IsFinite(fps) && fps > 0f
            ? fps
            : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(LibVlcDiagnosticsController));
        }
    }
}
