using Eizo.Playback.Core;
using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

public sealed class LibVlcPlaybackEngine : IPlaybackEngine
{
    private static readonly object NativeLifetimeGate = new();
    private static readonly Lazy<bool> NativeInitialized = new(() =>
    {
        LibVLCSharp.Shared.Core.Initialize();
        return true;
    });
    private int _mediaGeneration;
    private readonly LibVLCSharp.Shared.LibVLC _libVlc;
    private readonly PlaybackOperationQueue _operations = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly MediaPlayer _mediaPlayer;
    private readonly PlaybackStateMachine _stateMachine = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly LibVlcTrackController _trackController;
    private readonly LibVlcNavigationController _navigationController;
    private readonly LibVlcDiagnosticsController _diagnosticsController;

    private long _positionMilliseconds;
    private long _durationMilliseconds;
    private double _volume = 1d;
    private double _rate = 1d;
    private bool _hasMedia;
    private AuthenticatedHttpMediaInput? _authenticatedHttpInput;
    private RandomAccessMediaInput? _randomAccessMediaInput;
    private int _disposeRequested;
    private int _disposeState;

    public LibVlcPlaybackEngine(LibVlcPlaybackOptions? options = null)
    {
        options ??= new LibVlcPlaybackOptions();

        LibVLCSharp.Shared.LibVLC? libVlc = null;
        MediaPlayer? mediaPlayer = null;

        try
        {
            _ = NativeInitialized.Value;

            lock (NativeLifetimeGate)
            {
                libVlc = new LibVLCSharp.Shared.LibVLC(
                    options.EnableDebugLogs,
                    options.Arguments.ToArray());
            }

            mediaPlayer = new MediaPlayer(libVlc)
            {
                EnableHardwareDecoding = options.EnableHardwareDecoding
            };

            mediaPlayer.EnableKeyInput = false;
            mediaPlayer.EnableMouseInput = false;

            _libVlc = libVlc;
            _mediaPlayer = mediaPlayer;
            _trackController = new LibVlcTrackController(mediaPlayer, _operations);
            _navigationController = new LibVlcNavigationController(mediaPlayer, _operations);
            _diagnosticsController = new LibVlcDiagnosticsController(
                libVlc,
                mediaPlayer,
                _trackController,
                options, _operations);

            HookEvents();
            _stateMachine.StateChanged += OnStateMachineStateChanged;
        }
        catch (Exception exception)
        {
            mediaPlayer?.Dispose();
            lock (NativeLifetimeGate) libVlc?.Dispose();

            throw new PlaybackException(
                PlaybackErrorCode.BackendInitializationFailed,
                "Failed to initialize the LibVLC playback backend.",
                exception);
        }
    }

    public PlaybackState State => _stateMachine.State;

    internal MediaPlayer NativeMediaPlayer
    {
        get
        {
            ThrowIfDisposed();
            return _mediaPlayer;
        }
    }

    public IPlaybackTrackController Tracks
    {
        get
        {
            ThrowIfDisposed();
            return _trackController;
        }
    }

    public IPlaybackNavigationController Navigation
    {
        get
        {
            ThrowIfDisposed();
            return _navigationController;
        }
    }

    public IPlaybackDiagnosticsController Diagnostics
    {
        get
        {
            ThrowIfDisposed();
            return _diagnosticsController;
        }
    }

    public TimeSpan Position
    {
        get
        {
            ThrowIfDisposed();
            return FromMillisecondsOrZero(Interlocked.Read(ref _positionMilliseconds));
        }
    }

    public TimeSpan Duration
    {
        get
        {
            ThrowIfDisposed();
            return FromMillisecondsOrZero(Interlocked.Read(ref _durationMilliseconds));
        }
    }

    public double Volume
    {
        get
        {
            ThrowIfDisposed();
            return Volatile.Read(ref _volume);
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

            _operations.Post("volume", () =>
            {
                ThrowIfDisposed();
                _mediaPlayer.Volume = (int)Math.Round(value * 100d, MidpointRounding.AwayFromZero);
                Volatile.Write(ref _volume, value);
            });
        }
    }

    public double PlaybackRate
    {
        get
        {
            ThrowIfDisposed();
            return Volatile.Read(ref _rate);
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

            _operations.Post("rate", () =>
            {
                ThrowIfDisposed();
                if (_mediaPlayer.SetRate((float)value) != 0)
                    TransitionToFailure(CreateBackendException(PlaybackErrorCode.BackendFailure, "LibVLC rejected playback rate."));
                else Volatile.Write(ref _rate, value);
            });
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _operations.RunAsync("LibVlcPlaybackEngine.OpenAsync", () => OpenCoreAsync(source, linked.Token), linked.Token);
    }

    private async ValueTask OpenCoreAsync(
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

            Interlocked.Increment(ref _mediaGeneration);
            if (_hasMedia)
            {
                await StopCoreAsync(cancellationToken).ConfigureAwait(false);
                _mediaPlayer.Media = null;
                _hasMedia = false;
                DisposeMediaInputs();
            }

            Interlocked.Exchange(ref _positionMilliseconds, 0);
            Interlocked.Exchange(ref _durationMilliseconds, 0);
            _stateMachine.SetState(PlaybackState.Opening);

            try
            {
                using var media = CreateMedia(source, cancellationToken);
                _trackController.Reset();
                _navigationController.Reset();
                _mediaPlayer.Media = media;
                _hasMedia = true;
                _diagnosticsController.SetSource(source);

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
                DisposeMediaInputs();

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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _operations.RunAsync("LibVlcPlaybackEngine.PlayAsync", () => PlayCoreAsync(linked.Token), linked.Token);
    }

    private async ValueTask PlayCoreAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();
            EnsureMedia();

            if (State == PlaybackState.Ended)
            {
                await ResetEndedMediaAsync(cancellationToken).ConfigureAwait(false);
            }

            _stateMachine.SetState(PlaybackState.Opening);

            if (!_mediaPlayer.Play())
            {
                var exception = CreateBackendException(
                    PlaybackErrorCode.PlayFailed,
                    "LibVLC rejected the play request.");

                TransitionToFailure(exception);
                throw exception;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _operations.RunAsync("LibVlcPlaybackEngine.PauseAsync", () => PauseCoreAsync(linked.Token), linked.Token);
    }

    private async ValueTask PauseCoreAsync(CancellationToken cancellationToken = default)
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

            if (State is PlaybackState.Stopped
                or PlaybackState.Ended
                or PlaybackState.Failed)
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _operations.RunAsync("LibVlcPlaybackEngine.StopAsync", () => StopCommandCoreAsync(linked.Token), linked.Token);
    }

    private async ValueTask StopCommandCoreAsync(CancellationToken cancellationToken = default)
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
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _operations.RunAsync("LibVlcPlaybackEngine.SeekAsync", () => SeekCoreAsync(position, linked.Token), linked.Token);
    }

    private async ValueTask SeekCoreAsync(
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

            if (previousState == PlaybackState.Ended)
            {
                await ResetEndedMediaAsync(cancellationToken).ConfigureAwait(false);

                if (target <= TimeSpan.Zero)
                {
                    return;
                }

                _stateMachine.SetState(PlaybackState.Opening);

                if (!_mediaPlayer.Play())
                {
                    var exception = CreateBackendException(
                        PlaybackErrorCode.PlayFailed,
                        "LibVLC rejected the restart required for seeking from the ended state.");

                    TransitionToFailure(exception);
                    throw exception;
                }

                for (var attempt = 0; attempt < 40 && !_mediaPlayer.IsSeekable; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }

                if (!_mediaPlayer.IsSeekable)
                {
                    throw new PlaybackException(
                        PlaybackErrorCode.NotSeekable,
                        "The current media source did not become seekable after restarting from the ended state.");
                }

                _stateMachine.SetState(PlaybackState.Seeking);
                _mediaPlayer.SeekTo(target);
            Interlocked.Exchange(ref _positionMilliseconds, (long)target.TotalMilliseconds);
                _mediaPlayer.SetPause(true);

                PositionChanged?.Invoke(
                    this,
                    new PlaybackPositionChangedEventArgs(target));

                _stateMachine.SetState(PlaybackState.Paused);
                return;
            }

            _stateMachine.SetState(PlaybackState.Seeking);
            _mediaPlayer.SeekTo(target);
            Interlocked.Exchange(ref _positionMilliseconds, (long)target.TotalMilliseconds);

            PositionChanged?.Invoke(
                this,
                new PlaybackPositionChangedEventArgs(target));

            _stateMachine.SetState(previousState switch
            {
                PlaybackState.Paused => PlaybackState.Paused,
                PlaybackState.Playing or PlaybackState.Buffering => PlaybackState.Playing,
                _ => previousState
            });
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeRequested,
                1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        // Cancel every queued/in-flight operation and interrupt native inputs
        // so disposal can never hang behind a stuck open/read.
        _lifetime.Cancel();
        InterruptMediaInputs();
        return new(_operations.CompleteAsync(DisposeCoreAsync));
    }

    private async ValueTask DisposeCoreAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);

        try
        {
            UnhookEvents();
            _stateMachine.StateChanged -= OnStateMachineStateChanged;
            await _diagnosticsController.DisposeAsync().ConfigureAwait(false);
            await _trackController.DisposeAsync().ConfigureAwait(false);
            await _navigationController.DisposeAsync().ConfigureAwait(false);
            InterruptMediaInputs();
            if (_hasMedia)
            {
                await Task.Run(_mediaPlayer.Stop).ConfigureAwait(false);
                _mediaPlayer.Media = null;
                _hasMedia = false;
            }

            DisposeMediaInputs();


            _mediaPlayer.Dispose();
            lock (NativeLifetimeGate) _libVlc.Dispose();
        }
        finally
        {
            _operationGate.Release();
            _operationGate.Dispose();
            _lifetime.Dispose();
        }
    }

    private async Task ResetEndedMediaAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InterruptMediaInputs();
        await Task.Run(_mediaPlayer.Stop, cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _positionMilliseconds, 0);
        _stateMachine.SetState(PlaybackState.Stopped);
        PositionChanged?.Invoke(
            this,
            new PlaybackPositionChangedEventArgs(TimeSpan.Zero));
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        InterruptMediaInputs();
        await Task.Run(_mediaPlayer.Stop).ConfigureAwait(false);

        Interlocked.Exchange(ref _positionMilliseconds, 0);
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

    private void PostMediaEvent(string name, Action action)
    {
        var generation = Volatile.Read(ref _mediaGeneration);
        _operations.Post("event:" + name, () =>
        {
            if (generation == Volatile.Read(ref _mediaGeneration) && Volatile.Read(ref _disposeState) == 0) action();
        });
    }

    private void ReconcileNativeState()
    {
        var state = _mediaPlayer.State switch
        {
            VLCState.Opening => PlaybackState.Opening,
            VLCState.Buffering => PlaybackState.Buffering,
            VLCState.Playing => PlaybackState.Playing,
            VLCState.Paused => PlaybackState.Paused,
            VLCState.Stopped => PlaybackState.Stopped,
            VLCState.Ended => PlaybackState.Ended,
            VLCState.Error => PlaybackState.Failed,
            _ => State
        };
        _stateMachine.SetState(state);
    }

    private void OnOpening(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnOpening", () => OnOpeningCore(sender, eventArgs));

    private void OnOpeningCore(object? sender, EventArgs eventArgs) =>
        ReconcileNativeState();

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs eventArgs) =>
        PostMediaEvent("OnBuffering", () => OnBufferingCore(sender, eventArgs));

    private void OnBufferingCore(object? sender, MediaPlayerBufferingEventArgs eventArgs) => ReconcileNativeState();

    private void OnPlaying(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnPlaying", () => OnPlayingCore(sender, eventArgs));

    private void OnPlayingCore(object? sender, EventArgs eventArgs) =>
        ReconcileNativeState();

    private void OnPaused(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnPaused", () => OnPausedCore(sender, eventArgs));

    private void OnPausedCore(object? sender, EventArgs eventArgs) =>
        ReconcileNativeState();

    private void OnStopped(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnStopped", () => OnStoppedCore(sender, eventArgs));

    private void OnStoppedCore(object? sender, EventArgs eventArgs) => ReconcileNativeState();

    private void OnEndReached(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnEndReached", () => OnEndReachedCore(sender, eventArgs));

    private void OnEndReachedCore(object? sender, EventArgs eventArgs) =>
        ReconcileNativeState();

    private void OnEncounteredError(object? sender, EventArgs eventArgs) =>
        PostMediaEvent("OnEncounteredError", () => OnEncounteredErrorCore(sender, eventArgs));

    private void OnEncounteredErrorCore(object? sender, EventArgs eventArgs)
    {
        var exception = CreateBackendException(
            PlaybackErrorCode.BackendFailure,
            "LibVLC encountered an error during playback.");

        TransitionToFailure(exception);
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs eventArgs) =>
        PostMediaEvent("OnTimeChanged", () => OnTimeChangedCore(sender, eventArgs));

    private void OnTimeChangedCore(object? sender, MediaPlayerTimeChangedEventArgs eventArgs)
    {
        Interlocked.Exchange(ref _positionMilliseconds, eventArgs.Time);
        PositionChanged?.Invoke(this, new PlaybackPositionChangedEventArgs(FromMillisecondsOrZero(eventArgs.Time)));
    }

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs eventArgs) =>
        PostMediaEvent("OnLengthChanged", () => OnLengthChangedCore(sender, eventArgs));

    private void OnLengthChangedCore(object? sender, MediaPlayerLengthChangedEventArgs eventArgs)
    {
        Interlocked.Exchange(ref _durationMilliseconds, eventArgs.Length);
        DurationChanged?.Invoke(this, new PlaybackDurationChangedEventArgs(FromMillisecondsOrZero(eventArgs.Length)));
    }

    private void OnStateMachineStateChanged(
        object? sender,
        PlaybackStateChangedEventArgs eventArgs)
    {
        PlaybackTrace.Write("engine", "state", "changed", $"{eventArgs.PreviousState}->{eventArgs.CurrentState}");
        StateChanged?.Invoke(this, eventArgs);
    }

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

    private Media CreateMedia(PlaybackSource source, CancellationToken cancellationToken)
    {
        if (source.RandomAccessSource is { } randomAccessSource)
        {
            var input = new RandomAccessMediaInput(
                randomAccessSource,
                cancellationToken);

            try
            {
                var media = new Media(
                    _libVlc,
                    input);

                _randomAccessMediaInput = input;
                return media;
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        if (source.NetworkAccess is
            {
                HasCredentials: true
            } access &&
            source.Uri.Scheme is "http" or "https")
        {
            var input = new AuthenticatedHttpMediaInput(
                source.Uri,
                access, cancellationToken);

            try
            {
                var media = new Media(
                    _libVlc,
                    input);

                _authenticatedHttpInput = input;
                return media;
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        return new Media(
            _libVlc,
            source.Uri);
    }

    private void InterruptMediaInputs()
    {
        Volatile.Read(ref _authenticatedHttpInput)?.Interrupt();
        Volatile.Read(ref _randomAccessMediaInput)?.Interrupt();
    }

    private void DisposeMediaInputs()
    {
        var authenticated = Interlocked.Exchange(
            ref _authenticatedHttpInput,
            null);
        authenticated?.Dispose();

        var randomAccess = Interlocked.Exchange(
            ref _randomAccessMediaInput,
            null);
        randomAccess?.Dispose();
    }

    private static void ValidateSource(PlaybackSource source)
    {
        if (!source.Uri.IsAbsoluteUri)
        {
            throw new PlaybackException(
                PlaybackErrorCode.InvalidSource,
                "Playback source URI must be absolute.");
        }

        if (!string.IsNullOrEmpty(source.Uri.UserInfo))
        {
            throw new PlaybackException(
                PlaybackErrorCode.InvalidSource,
                "Playback source URI must not embed credentials.");
        }

        if (source.RandomAccessSource is null &&
            source.Uri.IsFile &&
            !File.Exists(source.Uri.LocalPath))
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
