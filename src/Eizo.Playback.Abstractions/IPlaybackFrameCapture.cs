namespace Eizo.Playback;

/// <summary>
/// Optional playback capability for capturing the currently decoded video frame.
/// The returned bytes contain a PNG image. Backends may return <see langword="null"/>
/// when the current source has no video output or a frame is not available yet.
/// </summary>
public interface IPlaybackFrameCapture
{
    ValueTask<byte[]?> CaptureFrameAsync(
        uint width = 320,
        uint height = 180,
        CancellationToken cancellationToken = default);
}
