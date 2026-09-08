# WinUI integration

## Why the WinUI layer owns LibVLC creation

LibVLC 3 requires the WinUI swap-chain pointers at native LibVLC construction time.

LibVLCSharp's WinUI `VideoView` creates the D3D11 swap chain after the control has a real size and raises `Initialized` with two required arguments:

- `--winrt-d3dcontext=...`
- `--winrt-swapchain=...`

Therefore the application must not construct a normal LibVLC engine first and try to attach a WinUI video surface later.

`Eizo.Playback.LibVLC.WinUI` solves this by owning the surface bootstrap:

```text
WinUI PlaybackView
      |
      | VideoView.Initialized
      v
SwapChainOptions
      |
      v
LibVlcPlaybackEngine
      |
      v
LibVLC + MediaPlayer
```

The public application still receives only `IPlaybackEngine`.

## XAML usage

```xml
xmlns:playback="using:Eizo.Playback.WinUI"

<playback:PlaybackView
    x:Name="PlaybackSurface" />
```

Then observe `EngineChanged` from the page or a composition/root layer and pass the current `IPlaybackEngine` to the player view model.

```csharp
PlaybackSurface.EngineChanged += (_, args) =>
{
    ViewModel.PlaybackEngine = args.CurrentEngine;
};
```

Do not reference `LibVLCSharp.Platforms.Windows.VideoView` from the Eizo application.

## Lifecycle

The WinUI `VideoView` destroys its D3D swap chain when unloaded.

Because a LibVLC 3 instance was constructed with pointers to that exact swap chain, `PlaybackView` disposes its current engine when unloaded. If the control is later loaded again and a new swap chain is created, a new engine is created and `EngineChanged` is raised.

The application should treat the engine provided by the surface as scoped to that live surface.

For deterministic shutdown, call `DisposeAsync` before permanently removing the surface when practical.

## Options

Configure `PlaybackOptions` before the surface is initialized.

User-supplied `--winrt-d3dcontext` and `--winrt-swapchain` arguments are ignored by the WinUI integration. The actual arguments produced by the live `VideoView` always win.

## Threading

`EngineChanged` and `InitializationFailed` originate from the WinUI surface lifecycle and are normally raised on the UI thread.

Events coming from `IPlaybackEngine` itself still have no UI-thread affinity and must be marshalled before updating WinUI-bound state.
