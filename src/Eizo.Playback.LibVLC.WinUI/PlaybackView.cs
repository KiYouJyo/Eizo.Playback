using Eizo.Playback.Backends.LibVLC;
using Eizo.Playback.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eizo.Playback.WinUI;

public sealed class PlaybackView : Grid, IAsyncDisposable
{
    private readonly LibVlcSwapChainSurface _surface;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _releaseGate = new(1, 1);
    private LibVlcPlaybackEngine? _engine;
    private LibVlcPlaybackOptions _playbackOptions = new();
    private bool _loaded;
    private bool _disposed;
    private Task? _disposeTask;

    public PlaybackView()
    {
        _surface = new LibVlcSwapChainSurface
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Children.Add(_surface);
        _surface.Initialized += OnSurfaceInitialized;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public IPlaybackEngine? Engine => _engine;
    public bool IsReady => Engine is not null;
    public LibVlcPlaybackOptions PlaybackOptions
    {
        get => _playbackOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Engine is not null) throw new InvalidOperationException("Configure playback options before initialization.");
            _playbackOptions = value;
        }
    }

    public event EventHandler<PlaybackViewEngineChangedEventArgs>? EngineChanged;
    public event EventHandler<PlaybackViewInitializationFailedEventArgs>? InitializationFailed;

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _loaded = false;
        return new ValueTask(_disposeTask ??= ReleaseAsync(final: true));
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed) return;
        _loaded = true;
        try
        {
            await _lifecycleGate.WaitAsync();
            try { if (_loaded && !_disposed) _surface.Activate(); }
            finally { _lifecycleGate.Release(); }
        }
        catch (Exception exception) { Report(exception); }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _loaded = false;
        try { await ReleaseAsync(final: false); }
        catch (Exception exception) { Report(exception); }
    }

    private async void OnSurfaceInitialized(object? sender, LibVlcSwapChainSurfaceInitializedEventArgs args)
    {
        try
        {
            await _lifecycleGate.WaitAsync();
            try
            {
                if (!_loaded || _disposed || _engine is not null) return;
                var options = LibVlcSurfaceOptions.Compose(_playbackOptions, args.SwapChainOptions);
                var engine = await Task.Run(() => new LibVlcPlaybackEngine(options));
                if (!_loaded || _disposed)
                {
                    await engine.DisposeAsync();
                    return;
                }
                _engine = engine;
                EngineChanged?.Invoke(this, new PlaybackViewEngineChangedEventArgs(null, engine));
            }
            finally { _lifecycleGate.Release(); }
        }
        catch (Exception exception) { Report(exception); }
    }

    private async Task ReleaseAsync(bool final)
    {
        // Serialize releases, but never hold _lifecycleGate across engine disposal:
        // a stuck native command would otherwise block every later Loaded/Initialized
        // transition and deadlock the whole surface lifecycle.
        await _releaseGate.WaitAsync();
        try
        {
            LibVlcPlaybackEngine? previous;
            await _lifecycleGate.WaitAsync();
            try
            {
                if (!final && _loaded) return;
                previous = _engine;
                _engine = null;
            }
            finally
            {
                _lifecycleGate.Release();
            }

            if (previous is not null)
            {
                EngineChanged?.Invoke(this, new PlaybackViewEngineChangedEventArgs(previous, null));
                await previous.DisposeAsync();
            }
            await _surface.DeactivateAsync();
            if (final)
            {
                _surface.Initialized -= OnSurfaceInitialized;
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;
                await _surface.DisposeAsync();
            }
        }
        finally
        {
            _releaseGate.Release();
        }
    }

    private void Report(Exception exception)
    {
        PlaybackTrace.Write("surface", "lifecycle", "error", exception.GetType().Name);
        InitializationFailed?.Invoke(this, new PlaybackViewInitializationFailedEventArgs(exception));
    }
}
