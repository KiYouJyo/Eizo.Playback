using System.Runtime.InteropServices;
using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class RandomAccessMediaInputTests
{
    [Fact]
    public void ReadsAndSeeksThroughHostSource()
    {
        var source = new BufferSource(
            Enumerable.Range(0, 64)
                .Select(static value => (byte)value)
                .ToArray());

        using var input = new RandomAccessMediaInput(source);

        Assert.True(input.Open(out var size));
        Assert.Equal(64UL, size);
        Assert.True(input.CanSeek);

        var pointer = Marshal.AllocHGlobal(8);
        try
        {
            Assert.Equal(8, input.Read(pointer, 8));
            var first = new byte[8];
            Marshal.Copy(pointer, first, 0, first.Length);
            Assert.Equal(
                Enumerable.Range(0, 8).Select(static x => (byte)x),
                first);

            Assert.True(input.Seek(16));
            Assert.Equal(8, input.Read(pointer, 8));
            var second = new byte[8];
            Marshal.Copy(pointer, second, 0, second.Length);
            Assert.Equal(
                Enumerable.Range(16, 8).Select(static x => (byte)x),
                second);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private sealed class BufferSource(byte[] data)
        : IPlaybackRandomAccessSource
    {
        public ValueTask<PlaybackRandomAccessInfo> GetInfoAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                new PlaybackRandomAccessInfo(
                    data.LongLength,
                    CanSeek: true));

        public ValueTask<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (offset >= data.LongLength)
                return ValueTask.FromResult(0);

            var count = (int)Math.Min(
                buffer.Length,
                data.LongLength - offset);
            data.AsMemory((int)offset, count)
                .CopyTo(buffer);
            return ValueTask.FromResult(count);
        }
    }
}
