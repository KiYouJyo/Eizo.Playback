using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcCodecFormatterTests
{
    [Fact]
    public void FromFourCcFormatsPrintableAscii()
    {
        const uint h264 = (uint)'h'
            | ((uint)'2' << 8)
            | ((uint)'6' << 16)
            | ((uint)'4' << 24);

        Assert.Equal("H264", LibVlcCodecFormatter.FromFourCc(h264));
    }

    [Fact]
    public void FromFourCcReturnsNullForZero()
    {
        Assert.Null(LibVlcCodecFormatter.FromFourCc(0));
    }

    [Fact]
    public void FromFourCcFallsBackToHexForBinaryValue()
    {
        Assert.Equal("0x00000001", LibVlcCodecFormatter.FromFourCc(1));
    }
}
