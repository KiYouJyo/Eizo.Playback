using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;

namespace Eizo.Playback.WinUI;

internal sealed class LibVlcSwapChainSurface : Grid, IDisposable
{
    private static readonly Guid SwapChainWidthKey =
        new(0xf1b59347, 0x1643, 0x411a, 0xad, 0x6b, 0xc7, 0x80, 0x17, 0x7a, 0x06, 0xb6);

    private static readonly Guid SwapChainHeightKey =
        new(0x6ea976a0, 0x9d60, 0x4bb7, 0xa5, 0xa9, 0x7d, 0xd1, 0x18, 0x7f, 0xc9, 0xbd);

    private readonly SwapChainPanel _panel;

    private SharpDX.Direct3D11.Device? _d3dDevice;
    private DeviceContext? _deviceContext;
    private SharpDX.DXGI.Device3? _dxgiDevice3;
    private SwapChain1? _swapChain;
    private SwapChain2? _swapChain2;

    private bool _active;
    private bool _creating;
    private int _disposeState;

    public LibVlcSwapChainSurface()
    {
        _panel = new SwapChainPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        Children.Add(_panel);

        _panel.SizeChanged += OnPanelSizeChanged;
        _panel.CompositionScaleChanged += OnPanelCompositionScaleChanged;
    }

    public event EventHandler<LibVlcSwapChainSurfaceInitializedEventArgs>? Initialized;

    public void Activate()
    {
        ThrowIfDisposed();
        _active = true;
        TryCreateSurface();
    }

    public void Deactivate()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        _active = false;
        DestroySurface();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _active = false;
        _panel.SizeChanged -= OnPanelSizeChanged;
        _panel.CompositionScaleChanged -= OnPanelCompositionScaleChanged;
        DestroySurface();
    }

    private void OnPanelSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        if (!_active || Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        if (_swapChain is null)
        {
            TryCreateSurface();
            return;
        }

        UpdateSizeMetadata();
    }

    private void OnPanelCompositionScaleChanged(
        SwapChainPanel sender,
        object eventArgs)
    {
        if (!_active || Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        UpdateScale();
        UpdateSizeMetadata();
    }

    private void TryCreateSurface()
    {
        if (!_active ||
            _creating ||
            _swapChain is not null ||
            Volatile.Read(ref _disposeState) != 0 ||
            _panel.ActualWidth <= 0d ||
            _panel.ActualHeight <= 0d)
        {
            return;
        }

        _creating = true;

        try
        {
            CreateSurface();

            var options = new[]
            {
                $"--winrt-d3dcontext=0x{_deviceContext!.NativePointer.ToString("x")}",
                $"--winrt-swapchain=0x{_swapChain!.NativePointer.ToString("x")}"
            };

            Initialized?.Invoke(
                this,
                new LibVlcSwapChainSurfaceInitializedEventArgs(options));
        }
        catch
        {
            DestroySurface();
            throw;
        }
        finally
        {
            _creating = false;
        }
    }

    private void CreateSurface()
    {
        using var factory = new Factory2(false);

        var preferredFlags =
            DeviceCreationFlags.BgraSupport |
            DeviceCreationFlags.VideoSupport;

        _d3dDevice =
            TryCreateHardwareDevice(factory, preferredFlags) ??
            TryCreateHardwareDevice(factory, DeviceCreationFlags.BgraSupport) ??
            TryCreateWarpDevice();

        if (_d3dDevice is null)
        {
            throw new VLCException(
                "Could not create a Direct3D 11 device for the WinUI playback surface.");
        }

        using var dxgiDevice = _d3dDevice.QueryInterface<SharpDX.DXGI.Device1>();

        var description = new SwapChainDescription1
        {
            Width = Math.Max(
                1,
                (int)Math.Ceiling(_panel.ActualWidth * _panel.CompositionScaleX)),
            Height = Math.Max(
                1,
                (int)Math.Ceiling(_panel.ActualHeight * _panel.CompositionScaleY)),
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            Usage = Usage.RenderTargetOutput,
            BufferCount = 2,
            SwapEffect = SwapEffect.FlipSequential,
            Scaling = Scaling.Stretch,
            AlphaMode = AlphaMode.Unspecified,
            Flags = SwapChainFlags.None
        };

        _swapChain = new SwapChain1(
            factory,
            _d3dDevice,
            ref description);

        dxgiDevice.MaximumFrameLatency = 1;

        using (var panelNative = ComObject.As<SwapChainPanelNative>(_panel))
        {
            panelNative.SwapChain = _swapChain;
        }

        _dxgiDevice3 = dxgiDevice.QueryInterface<SharpDX.DXGI.Device3>();
        _swapChain2 = _swapChain.QueryInterface<SwapChain2>();
        _deviceContext = _d3dDevice.ImmediateContext;

        UpdateScale();
        UpdateSizeMetadata();
    }

    private static SharpDX.Direct3D11.Device? TryCreateHardwareDevice(
        Factory2 factory,
        DeviceCreationFlags flags)
    {
        for (var index = 0; index < factory.GetAdapterCount(); index++)
        {
            using var adapter = factory.GetAdapter(index);

            try
            {
                return new SharpDX.Direct3D11.Device(adapter, flags);
            }
            catch (SharpDXException)
            {
            }
        }

        return null;
    }

    private static SharpDX.Direct3D11.Device? TryCreateWarpDevice()
    {
        try
        {
            return new SharpDX.Direct3D11.Device(
                DriverType.Warp,
                DeviceCreationFlags.BgraSupport);
        }
        catch (SharpDXException)
        {
            return null;
        }
    }

    private void UpdateScale()
    {
        if (_swapChain2 is null ||
            _swapChain2.IsDisposed ||
            _panel.CompositionScaleX <= 0f ||
            _panel.CompositionScaleY <= 0f)
        {
            return;
        }

        _swapChain2.MatrixTransform = new RawMatrix3x2
        {
            M11 = 1f / _panel.CompositionScaleX,
            M22 = 1f / _panel.CompositionScaleY
        };
    }

    private void UpdateSizeMetadata()
    {
        if (_swapChain is null || _swapChain.IsDisposed)
        {
            return;
        }

        var widthPointer = IntPtr.Zero;
        var heightPointer = IntPtr.Zero;

        try
        {
            widthPointer = Marshal.AllocHGlobal(sizeof(int));
            heightPointer = Marshal.AllocHGlobal(sizeof(int));

            var width = Math.Max(
                1,
                (int)Math.Ceiling(_panel.ActualWidth * _panel.CompositionScaleX));
            var height = Math.Max(
                1,
                (int)Math.Ceiling(_panel.ActualHeight * _panel.CompositionScaleY));

            Marshal.WriteInt32(widthPointer, width);
            Marshal.WriteInt32(heightPointer, height);

            _swapChain.SetPrivateData(
                SwapChainWidthKey,
                sizeof(int),
                widthPointer);

            _swapChain.SetPrivateData(
                SwapChainHeightKey,
                sizeof(int),
                heightPointer);
        }
        finally
        {
            if (widthPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(widthPointer);
            }

            if (heightPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(heightPointer);
            }
        }
    }

    private void DestroySurface()
    {
        try
        {
            using var panelNative = ComObject.As<SwapChainPanelNative>(_panel);
            panelNative.SwapChain = null;
        }
        catch
        {
        }

        _swapChain2?.Dispose();
        _swapChain2 = null;

        _dxgiDevice3?.Dispose();
        _dxgiDevice3 = null;

        _deviceContext?.Dispose();
        _deviceContext = null;

        _swapChain?.Dispose();
        _swapChain = null;

        _d3dDevice?.Dispose();
        _d3dDevice = null;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(LibVlcSwapChainSurface));
        }
    }

    [Guid("63AAD0B8-7C24-40FF-85A8-640D944CC325")]
    private sealed class SwapChainPanelNative : SharpDX.DXGI.ISwapChainPanelNative
    {
        public SwapChainPanelNative(IntPtr nativePointer)
            : base(nativePointer)
        {
        }
    }
}

internal sealed class LibVlcSwapChainSurfaceInitializedEventArgs : EventArgs
{
    public LibVlcSwapChainSurfaceInitializedEventArgs(
        IReadOnlyList<string> swapChainOptions)
    {
        SwapChainOptions = swapChainOptions ??
            throw new ArgumentNullException(nameof(swapChainOptions));
    }

    public IReadOnlyList<string> SwapChainOptions { get; }
}
