using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcSurfaceOptionsTests
{
    [Fact]
    public void ComposePreservesNormalUserArguments()
    {
        var source = new LibVlcPlaybackOptions
        {
            EnableDebugLogs = true,
            EnableHardwareDecoding = false,
            Arguments = ["--network-caching=1500", "--quiet"]
        };

        var result = LibVlcSurfaceOptions.Compose(
            source,
            [
                "--winrt-d3dcontext=0x1234",
                "--winrt-swapchain=0x5678"
            ]);

        Assert.True(result.EnableDebugLogs);
        Assert.False(result.EnableHardwareDecoding);
        Assert.Equal(
            [
                "--network-caching=1500",
                "--quiet",
                "--winrt-d3dcontext=0x1234",
                "--winrt-swapchain=0x5678"
            ],
            result.Arguments);
    }

    [Fact]
    public void ComposeReplacesUserSuppliedReservedSurfaceArguments()
    {
        var source = new LibVlcPlaybackOptions
        {
            Arguments =
            [
                "--winrt-d3dcontext=0xBAD",
                "--network-caching=1000",
                "--winrt-swapchain=0xBAD"
            ]
        };

        var result = LibVlcSurfaceOptions.Compose(
            source,
            [
                "--winrt-d3dcontext=0x1234",
                "--winrt-swapchain=0x5678"
            ]);

        Assert.Equal(
            [
                "--network-caching=1000",
                "--winrt-d3dcontext=0x1234",
                "--winrt-swapchain=0x5678"
            ],
            result.Arguments);
    }

    [Fact]
    public void ComposeRejectsMissingSurfaceArguments()
    {
        var source = new LibVlcPlaybackOptions();

        Assert.Throws<ArgumentException>(
            () => LibVlcSurfaceOptions.Compose(source, []));
    }
}
