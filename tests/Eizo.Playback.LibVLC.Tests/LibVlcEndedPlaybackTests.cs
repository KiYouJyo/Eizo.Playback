using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class LibVlcEndedPlaybackTests
{
    [Fact]
    public async Task PlayAfterEndedRestartsFromBeginning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try
        {
            await File.WriteAllBytesAsync(
                path,
                CreateWaveFile(durationMilliseconds: 700),
                cancellationToken);

            await using var engine = CreateHeadlessEngine();

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);
            await WaitForStateAsync(engine, PlaybackState.Ended, cancellationToken);

            await engine.PlayAsync(cancellationToken);
            await WaitForStateAsync(engine, PlaybackState.Playing, cancellationToken);

            Assert.NotEqual(PlaybackState.Ended, engine.State);
            Assert.True(engine.Position < TimeSpan.FromMilliseconds(500));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SeekToZeroAfterEndedResetsToStoppedAndCanReplay()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        try
        {
            await File.WriteAllBytesAsync(
                path,
                CreateWaveFile(durationMilliseconds: 700),
                cancellationToken);

            await using var engine = CreateHeadlessEngine();

            await engine.OpenAsync(
                PlaybackSource.FromFile(path),
                cancellationToken);

            await engine.PlayAsync(cancellationToken);
            await WaitForStateAsync(engine, PlaybackState.Ended, cancellationToken);

            await engine.SeekAsync(TimeSpan.Zero, cancellationToken);

            Assert.Equal(PlaybackState.Stopped, engine.State);
            Assert.Equal(TimeSpan.Zero, engine.Position);

            await engine.PlayAsync(cancellationToken);
            await WaitForStateAsync(engine, PlaybackState.Playing, cancellationToken);

            Assert.NotEqual(PlaybackState.Ended, engine.State);
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

    private static async Task WaitForStateAsync(
        LibVlcPlaybackEngine engine,
        PlaybackState state,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);

        while (engine.State != state)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Timed out waiting for playback state {state}. Current state: {engine.State}.");
            }

            await Task.Delay(25, cancellationToken);
        }
    }

    private static byte[] CreateWaveFile(int durationMilliseconds)
    {
        const int sampleRate = 8_000;
        const short channels = 1;
        const short bitsPerSample = 16;

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
