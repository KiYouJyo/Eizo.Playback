namespace Eizo.Playback.LibVLC.Tests;

public sealed class PlaybackSourceRandomAccessTests
{
    [Fact]
    public void FromRandomAccess_PreservesCanonicalUriWithoutCredentials()
    {
        var reader = new EmptySource();
        var uri = new Uri("https://example.test/media/file.mkv");

        var source = PlaybackSource.FromRandomAccess(
            uri,
            reader,
            "Example");

        Assert.Equal(uri, source.Uri);
        Assert.Same(reader, source.RandomAccessSource);
        Assert.Null(source.NetworkAccess);
        Assert.Equal("Example", source.DisplayName);
    }

    private sealed class EmptySource : IPlaybackRandomAccessSource
    {
        public ValueTask<PlaybackRandomAccessInfo> GetInfoAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                new PlaybackRandomAccessInfo(0, true));

        public ValueTask<int> ReadAsync(
            long offset,
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }
}
