using Eizo.Playback.Backends.LibVLC;
using LibVLCSharp.Platforms.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Playback.WinUI;

public sealed class PlaybackView : Grid, IAsyncDisposable
{
    private readonly VideoView _videoView;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private LibVlcPlaybackEngine? _engine;
    private LibVlcPlaybackOptions _playbackOptions = new();
    private int _disposeState;

    public PlaybackView()
    {
        _videoView = new VideoView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Children.Add(_videoView);

        _videoView.Initialized += OnVideoViewInitialized;
        Unloaded += OnUnloaded;
    }

    public IPlaybackEngine? Engine => Volatile.Read(ref _engine);

    public bool IsReady => Engine is not null;

    public LibVlcPlaybackOptions PlaybackOptions
    {
        get => _playbackOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ThrowIfDisposed();

            if (Engine is not null)
            {
                throw new InvalidOperationException(
                    "Playback options must be configured before the WinUI video surface is initialized.");
            }

            _playbackOptions = value;
        }
    }

    public event EventHandler<PlaybackViewEngineChangedEventArgs>? EngineChanged;

    public event EventHandler<PlaybackViewInitializationFailedEventArgs>? InitializationFailed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _videoView.Initialized -= OnVideoViewInitialized;
        Unloaded -= OnUnloaded;

        await ReleaseEngineAsync().ConfigureAwait(true);
    }

    private async void OnVideoViewInitialized(object? sender, InitializedEventArgs eventArgs)
    {
        try
        {
            await InitializeEngineAsync(eventArgs.SwapChainOptions).ConfigureAwait(true);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposeState) != 0)
        {
        }
        catch (Exception exception)
        {
            InitializationFailed?.Invoke(
                this,
                new PlaybackViewInitializationFailedEventArgs(exception));
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await ReleaseEngineAsync().ConfigureAwait(true);
        }
        catch (ObjectDisposedException) when (Volatile.Read(ref _disposeState) != 0)
        {
        }
        catch (Exception exception)
        {
            InitializationFailed?.Invoke(
                this,
                new PlaybackViewInitializationFailedEventArgs(exception));
        }
    }

    private async Task InitializeEngineAsync(IReadOnlyList<string> swapChainOptions)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(true);

        try
        {
            if (Volatile.Read(ref _disposeState) != 0)
            {
                return;
            }

            var previousEngine = Interlocked.Exchange(ref _engine, null);

            if (previousEngine is not null)
            {
                _videoView.MediaPlayer = null;
                await previousEngine.DisposeAsync().ConfigureAwait(true);
            }

            var options = LibVlcSurfaceOptions.Compose(
                _playbackOptions,
                swapChainOptions);

            var currentEngine = new LibVlcPlaybackEngine(options);

            _videoView.MediaPlayer = currentEngine.NativeMediaPlayer;
            Volatile.Write(ref _engine, currentEngine);

            EngineChanged?.Invoke(
                this,
                new PlaybackViewEngineChangedEventArgs(
                    previousEngine,
                    currentEngine));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ReleaseEngineAsync()
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(true);

        try
        {
            var previousEngine = Interlocked.Exchange(ref _engine, null);

            if (previousEngine is null)
            {
                return;
            }

            _videoView.MediaPlayer = null;
            await previousEngine.DisposeAsync().ConfigureAwait(true);

            EngineChanged?.Invoke(
                this,
                new PlaybackViewEngineChangedEventArgs(
                    previousEngine,
                    null));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(PlaybackView));
        }
    }
}
