using Eizo.Playback.Core;
using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

public sealed class LibVlcPlaybackEngine : IPlaybackEngine
{
    private readonly LibVLCSharp.Shared.LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private readonly PlaybackStateMachine _stateMachine = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    private bool _hasMedia;
    private int _disposeState;

    public LibVlcPlaybackEngine(LibVlcPlaybackOptions? options = null)
    {
        options ??= new LibVlcPlaybackOptions();

        LibVLCSharp.Shared.LibVLC? libVlc = null;
        MediaPlayer? mediaPlayer = null;

        try
        {
            LibVLCSharp.Shared.Core.Initialize();

            libVlc = new LibVLCSharp.Shared.LibVLC(
                options.EnableDebugLogs,
                options.Arguments.ToArray());

            mediaPlayer = new MediaPlayer(libVlc)
            {
                EnableHardwareDecoding = options.EnableHardwareDecoding
            };

            mediaPlayer.EnableKeyInput = false;
            mediaPlayer.EnableMouseInput = false;

            _libVlc = libVlc;
            _mediaPlayer = mediaPlayer;

            HookEvents();
            _stateMachine.StateChanged += OnStateMachineStateChanged;
        }
        catch (Exception exception)
        {
            mediaPlayer?.Dispose();
            libVlc?.Dispose();

            throw new PlaybackException(
                PlaybackErrorCode.BackendInitializationFailed,
                "Failed to initialize the LibVLC playback backend.",
                exception);
        }
    }

    public PlaybackState State => _stateMachine.State;

    public TimeSpan Position => FromMillisecondsOrZero(_mediaPlayer.Time);

    public TimeSpan Duration => FromMillisecondsOrZero(_mediaPlayer.Length);

    public double Volume
    {
        get
        {
            ThrowIfDisposed();
            return Math.Clamp(_mediaPlayer.Volume / 100d, 0d, 1d);
        }
        set
        {
            ThrowIfDisposed();

            if (!double.IsFinite(value) || value is < 0d or > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Volume must be a finite value between 0.0 and 1.0.");
            }

            _mediaPlayer.Volume = (int)Math.Round(value * 100d, MidpointRounding.AwayFromZero);
        }
    }

    public double PlaybackRate
    {
        get
        {
            ThrowIfDisposed();
            return _mediaPlayer.Rate;
        }
        set
        {
            ThrowIfDisposed();

            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Playback rate must be a finite positive value.");
            }

            if (_mediaPlayer.SetRate((float)value) != 0)
            {
                throw CreateBackendException(
                    PlaybackErrorCode.BackendFailure,
                    $"LibVLC rejected playback rate {value}.");
            }
        }
    }

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

    public event EventHandler<PlaybackDurationChangedEventArgs>? DurationChanged;

    public event EventHandler<PlaybackFailedEventArgs>? Failed;

    public async ValueTask OpenAsync(
        PlaybackSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfDisposed();

        ValidateSource(source);

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_hasMedia)
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
            }

            _stateMachine.SetState(PlaybackState.Opening);

            try
            {
                using var media = new Media(_libVlc, source.Uri);
                _mediaPlayer.Media = media;
                _hasMedia = true;

                _stateMachine.SetState(PlaybackState.Stopped);
                PositionChanged?.Invoke(
                    this,
                    new PlaybackPositionChangedEventArgs(TimeSpan.Zero));

                var duration = Duration;
                if (duration > TimeSpan.Zero)
                {
                    DurationChanged?.Invoke(
                        this,
                        new PlaybackDurationChangedEventArgs(duration));
                }
            }
            catch (Exception exception) when (exception is not PlaybackException)
            {
                throw CreateBackendException(
                    PlaybackErrorCode.OpenFailed,
                    $"Failed to open media source '{source.Uri}'.",
                    exception);
            }
        }
        catch (PlaybackException exception)
        {
            TransitionToFailure(exception);
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask PlayAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            EnsureMedia();

            _stateMachine.SetState(PlaybackState.Opening);

            if (!_mediaPlayer.Play())
            {
                var exception = CreateBackendException(
                    PlaybackErrorCode.PlayFailed,
                    "LibVLC rejected the play request.");

                TransitionToFailure(exception);
                throw exception;
            }

            _stateMachine.SetState(PlaybackState.Playing);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            EnsureMedia();

            if (State == PlaybackState.Paused)
            {
                return;
            }

            _mediaPlayer.SetPause(true);
            _stateMachine.SetState(PlaybackState.Paused);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (!_hasMedia)
            {
                return;
            }

            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask SeekAsync(
        TimeSpan position,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (position < TimeSpan.Zero)
        {
            throw new PlaybackException(
                PlaybackErrorCode.InvalidPosition,
                "Playback position cannot be negative.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            EnsureMedia();

            if (!_mediaPlayer.IsSeekable)
            {
                throw new PlaybackException(
                    PlaybackErrorCode.NotSeekable,
                    "The current media source is not seekable.");
            }

            var previousState = State;
            var duration = Duration;
            var target = duration > TimeSpan.Zero && position > duration
                ? duration
                : position;

            _stateMachine.SetState(PlaybackState.Seeking);
            _mediaPlayer.SeekTo(target);

            PositionChanged?.Invoke(
                this,
                new PlaybackPositionChangedEventArgs(target));

            _stateMachine.SetState(
                previousState == PlaybackState.Paused
                    ? PlaybackState.Paused
                    : PlaybackState.Playing);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (_hasMedia)
            {
                await Task.Run(_mediaPlayer.Stop).ConfigureAwait(false);
                _mediaPlayer.Media = null;
                _hasMedia = false;
            }

            UnhookEvents();
            _stateMachine.StateChanged -= OnStateMachineStateChanged;

            _mediaPlayer.Dispose();
            _libVlc.Dispose();
        }
        finally
        {
            _operationGate.Release();
            _operationGate.Dispose();
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Run(_mediaPlayer.Stop).ConfigureAwait(false);

        _stateMachine.SetState(PlaybackState.Stopped);
        PositionChanged?.Invoke(
            this,
            new PlaybackPositionChangedEventArgs(TimeSpan.Zero));
    }

    private void HookEvents()
    {
        _mediaPlayer.Opening += OnOpening;
        _mediaPlayer.Buffering += OnBuffering;
        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += OnPaused;
        _mediaPlayer.Stopped += OnStopped;
        _mediaPlayer.EndReached += OnEndReached;
        _mediaPlayer.EncounteredError += OnEncounteredError;
        _mediaPlayer.TimeChanged += OnTimeChanged;
        _mediaPlayer.LengthChanged += OnLengthChanged;
    }

    private void UnhookEvents()
    {
        _mediaPlayer.Opening -= OnOpening;
        _mediaPlayer.Buffering -= OnBuffering;
        _mediaPlayer.Playing -= OnPlaying;
        _mediaPlayer.Paused -= OnPaused;
        _mediaPlayer.Stopped -= OnStopped;
        _mediaPlayer.EndReached -= OnEndReached;
        _mediaPlayer.EncounteredError -= OnEncounteredError;
        _mediaPlayer.TimeChanged -= OnTimeChanged;
        _mediaPlayer.LengthChanged -= OnLengthChanged;
    }

    private void OnOpening(object? sender, EventArgs eventArgs) =>
        _stateMachine.SetState(PlaybackState.Opening);

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs eventArgs)
    {
        if (eventArgs.Cache < 100f)
        {
            _stateMachine.SetState(PlaybackState.Buffering);
        }
        else if (_mediaPlayer.IsPlaying)
        {
            _stateMachine.SetState(PlaybackState.Playing);
        }
    }

    private void OnPlaying(object? sender, EventArgs eventArgs) =>
        _stateMachine.SetState(PlaybackState.Playing);

    private void OnPaused(object? sender, EventArgs eventArgs) =>
        _stateMachine.SetState(PlaybackState.Paused);

    private void OnStopped(object? sender, EventArgs eventArgs)
    {
        if (State is not PlaybackState.Ended and not PlaybackState.Failed)
        {
            _stateMachine.SetState(PlaybackState.Stopped);
        }
    }

    private void OnEndReached(object? sender, EventArgs eventArgs) =>
        _stateMachine.SetState(PlaybackState.Ended);

    private void OnEncounteredError(object? sender, EventArgs eventArgs)
    {
        var exception = CreateBackendException(
            PlaybackErrorCode.BackendFailure,
            "LibVLC encountered an error during playback.");

        TransitionToFailure(exception);
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs eventArgs) =>
        PositionChanged?.Invoke(
            this,
            new PlaybackPositionChangedEventArgs(
                FromMillisecondsOrZero(eventArgs.Time)));

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs eventArgs) =>
        DurationChanged?.Invoke(
            this,
            new PlaybackDurationChangedEventArgs(
                FromMillisecondsOrZero(eventArgs.Length)));

    private void OnStateMachineStateChanged(
        object? sender,
        PlaybackStateChangedEventArgs eventArgs) =>
        StateChanged?.Invoke(this, eventArgs);

    private void TransitionToFailure(PlaybackException exception)
    {
        _stateMachine.SetState(PlaybackState.Failed);
        Failed?.Invoke(this, new PlaybackFailedEventArgs(exception));
    }

    private void EnsureMedia()
    {
        if (!_hasMedia)
        {
            throw new PlaybackException(
                PlaybackErrorCode.NoMedia,
                "No media source is currently open.");
        }
    }

    private static void ValidateSource(PlaybackSource source)
    {
        if (!source.Uri.IsAbsoluteUri)
        {
            throw new PlaybackException(
                PlaybackErrorCode.InvalidSource,
                "Playback source URI must be absolute.");
        }

        if (source.Uri.IsFile && !File.Exists(source.Uri.LocalPath))
        {
            throw new PlaybackException(
                PlaybackErrorCode.FileNotFound,
                $"Media file was not found: {source.Uri.LocalPath}");
        }
    }

    private PlaybackException CreateBackendException(
        PlaybackErrorCode code,
        string message,
        Exception? innerException = null)
    {
        var backendMessage = _libVlc.LastLibVLCError;

        var combinedMessage = string.IsNullOrWhiteSpace(backendMessage)
            ? message
            : $"{message} LibVLC: {backendMessage}";

        return innerException is null
            ? new PlaybackException(code, combinedMessage)
            : new PlaybackException(code, combinedMessage, innerException);
    }

    private static TimeSpan FromMillisecondsOrZero(long milliseconds) =>
        milliseconds > 0
            ? TimeSpan.FromMilliseconds(milliseconds)
            : TimeSpan.Zero;

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(LibVlcPlaybackEngine));
        }
    }
}
