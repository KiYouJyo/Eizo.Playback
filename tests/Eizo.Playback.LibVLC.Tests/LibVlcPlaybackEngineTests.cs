using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcPlaybackEngineTests
{
    [Fact]
    public async Task EngineInitializesInIdleState()
    {
        await using var engine = new LibVlcPlaybackEngine();

        Assert.Equal(PlaybackState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.Position);
        Assert.Equal(TimeSpan.Zero, engine.Duration);
    }

    [Fact]
    public async Task OpenMissingFileThrowsPlaybackException()
    {
        await using var engine = new LibVlcPlaybackEngine();

        var source = PlaybackSource.FromFile(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mkv"));

        var exception = await Assert.ThrowsAsync<PlaybackException>(
            async () => await engine.OpenAsync(
                source,
                TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackErrorCode.FileNotFound, exception.Code);
        Assert.Equal(PlaybackState.Idle, engine.State);
    }

    [Fact]
    public async Task OpenExistingFileTransitionsToStopped()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bin");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await File.WriteAllBytesAsync(
                path,
                [0x45, 0x49, 0x5A, 0x4F],
                cancellationToken);

            await using var engine = new LibVlcPlaybackEngine();

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            Assert.Equal(PlaybackState.Stopped, engine.State);

            await engine.PauseAsync(cancellationToken);

            Assert.Equal(PlaybackState.Stopped, engine.State);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PlaybackNetworkAccessRedactsPassword()
    {
        var access = new PlaybackNetworkAccess("alice", "super-secret");

        Assert.True(access.HasCredentials);
        Assert.Equal("alice", access.UserName);
        Assert.Equal("super-secret", access.Password);
        Assert.DoesNotContain("super-secret", access.ToString(), StringComparison.Ordinal);
        Assert.Contains("<redacted>", access.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FromUriRejectsEmbeddedCredentials()
    {
        var uri = new Uri("https://alice:secret@example.test/video.mkv");

        Assert.Throws<ArgumentException>(() =>
            PlaybackSource.FromUri(uri));
    }

    [Fact]
    public void FromUriAcceptsSeparateNetworkCredentials()
    {
        var source = PlaybackSource.FromUri(
            new Uri("https://example.test/video.mkv"),
            "video",
            new PlaybackNetworkAccess("alice", "secret"));

        Assert.Equal("https://example.test/video.mkv", source.Uri.AbsoluteUri);
        Assert.Equal("video", source.DisplayName);
        Assert.Equal("alice", source.NetworkAccess?.UserName);
        Assert.Equal("secret", source.NetworkAccess?.Password);
    }

    [Fact]
    public async Task DisposeAsyncIsIdempotent()
    {
        var engine = new LibVlcPlaybackEngine();

        await engine.DisposeAsync();
        await engine.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = engine.Position);
        Assert.Throws<ObjectDisposedException>(() => _ = engine.Duration);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public async Task VolumeRejectsValuesOutsideNormalizedRange(double value)
    {
        await using var engine = new LibVlcPlaybackEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Volume = value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public async Task PlaybackRateRejectsNonPositiveOrNonFiniteValues(double value)
    {
        await using var engine = new LibVlcPlaybackEngine();

        Assert.Throws<ArgumentOutOfRangeException>(() => engine.PlaybackRate = value);
    }
}
