using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcTrackControllerTests
{
    [Fact]
    public async Task UnknownAudioTrackIsRejected()
    {
        await using var engine = CreateHeadlessEngine();

        var exception = await Assert.ThrowsAsync<PlaybackException>(
            async () => await engine.Tracks.SelectAudioTrackAsync(
                999,
                TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackErrorCode.TrackNotFound, exception.Code);
    }

    [Fact]
    public async Task MissingExternalSubtitleIsRejected()
    {
        await using var engine = CreateHeadlessEngine();

        var source = new Uri(
            Path.Combine(
                Path.GetTempPath(),
                $"{Guid.NewGuid():N}.ass"));

        var exception = await Assert.ThrowsAsync<PlaybackException>(
            async () => await engine.Tracks.AddExternalSubtitleAsync(
                source,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackErrorCode.FileNotFound, exception.Code);
    }

    [Fact]
    public async Task PlayingWaveDiscoversAudioTrack()
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
            var tracksReady = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            engine.Tracks.TracksChanged += (_, args) =>
            {
                if (args.Kind is PlaybackTrackKind.Audio or PlaybackTrackKind.All
                    && engine.Tracks.AudioTracks.Count > 0)
                {
                    tracksReady.TrySetResult();
                }
            };

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);

            await tracksReady.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);

            await engine.Tracks.RefreshAsync(cancellationToken);

            var audioTrack = Assert.Single(engine.Tracks.AudioTracks);

            Assert.True(audioTrack.Id >= 0);
            Assert.True(audioTrack.IsSelected);
        }
        finally
        {
            File.Delete(path);
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
