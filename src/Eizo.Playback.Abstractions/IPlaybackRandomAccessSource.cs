namespace Eizo.Playback;

public interface IPlaybackRandomAccessSource
{
    ValueTask<PlaybackRandomAccessInfo> GetInfoAsync(
        CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(
        long offset,
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);
}
