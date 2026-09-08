using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcDiagnosticsControllerTests
{
    [Fact]
    public async Task InitialSnapshotReportsBackendWithoutMedia()
    {
        await using var engine = CreateHeadlessEngine();

        var snapshot = engine.Diagnostics.Current;

        Assert.Equal("LibVLC", snapshot.Backend.Name);
        var backendVersion = Assert.IsType<string>(snapshot.Backend.Version);
        Assert.StartsWith("3.", backendVersion);
        Assert.NotNull(snapshot.Backend.WrapperVersion);
        Assert.True(snapshot.Backend.HardwareDecodingRequested);
        Assert.Null(snapshot.Backend.HardwareDecodingActive);

        Assert.Equal(PlaybackInputKind.None, snapshot.InputKind);
        Assert.Null(snapshot.InputScheme);
        Assert.False(snapshot.Capabilities.CanSeek);
        Assert.False(snapshot.Capabilities.CanPause);
        Assert.Equal(0u, snapshot.Capabilities.VideoOutputCount);
        Assert.Null(snapshot.Capabilities.PlaybackFps);
        Assert.InRange(snapshot.Capabilities.BufferingPercent, 0d, 100d);
        Assert.Null(snapshot.Statistics);
    }

    [Fact]
    public async Task HardwareDecodeConfigurationIsReportedWithoutClaimingActivation()
    {
        await using var engine = new LibVlcPlaybackEngine(
            new LibVlcPlaybackOptions
            {
                EnableHardwareDecoding = false,
                Arguments = ["--aout=dummy", "--intf=dummy"]
            });

        var snapshot = engine.Diagnostics.Current;

        Assert.False(snapshot.Backend.HardwareDecodingRequested);
        Assert.Null(snapshot.Backend.HardwareDecodingActive);
    }

    [Fact]
    public async Task NetworkUriIsClassifiedWithoutExposingFullUri()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = CreateHeadlessEngine();

        var source = new PlaybackSource(
            new Uri("https://example.invalid/private/video.mkv?token=secret"));

        await engine.OpenAsync(source, cancellationToken);

        var snapshot = await engine.Diagnostics.RefreshAsync(cancellationToken);

        Assert.Equal(PlaybackInputKind.Network, snapshot.InputKind);
        Assert.Equal("https", snapshot.InputScheme);

        var serialized = snapshot.ToString();

        Assert.DoesNotContain("example.invalid", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=secret", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlayingWaveProducesRuntimeDiagnostics()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.wav");

        try
        {
            await File.WriteAllBytesAsync(
                path,
                CreateWaveFile(),
                cancellationToken);

            await using var engine = CreateHeadlessEngine();

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);

            await WaitForAudioTrackAsync(engine, cancellationToken);

            var snapshot = await engine.Diagnostics.RefreshAsync(cancellationToken);

            Assert.Equal(PlaybackInputKind.LocalFile, snapshot.InputKind);
            Assert.Equal("file", snapshot.InputScheme);

            Assert.True(snapshot.Capabilities.CanSeek);
            Assert.True(snapshot.Capabilities.CanPause);
            Assert.Equal(0u, snapshot.Capabilities.VideoOutputCount);
            Assert.Null(snapshot.Capabilities.PlaybackFps);
            Assert.InRange(snapshot.Capabilities.BufferingPercent, 0d, 100d);

            Assert.NotNull(snapshot.SelectedAudioTrack);
            Assert.Null(snapshot.SelectedVideoTrack);

            var statistics = Assert.IsType<PlaybackMediaStatistics>(snapshot.Statistics);
            Assert.True(statistics.ReadBytes >= 0);
            Assert.True(statistics.DemuxReadBytes >= 0);
            Assert.True(statistics.DecodedAudio >= 0);
            Assert.True(statistics.LostAudioBuffers >= 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task WaitForAudioTrackAsync(
        LibVlcPlaybackEngine engine,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (engine.Tracks.AudioTracks.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for audio track diagnostics.");
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(50),
                cancellationToken);

            await engine.Tracks.RefreshAsync(cancellationToken);
        }
    }

    private static LibVlcPlaybackEngine CreateHeadlessEngine() =>
        new(
            new LibVlcPlaybackOptions
            {
                Arguments =
                [
                    "--aout=dummy",
                    "--intf=dummy",
                    "--no-video-title-show"
                ]
            });

    private static byte[] CreateWaveFile()
    {
        const int sampleRate = 8_000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int durationMilliseconds = 500;

        var sampleCount = sampleRate * durationMilliseconds / 1_000;
        var dataLength = sampleCount * channels * (bitsPerSample / 8);
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);

        return stream.ToArray();
    }
}
