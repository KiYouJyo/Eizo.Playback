namespace Eizo.Playback.Backends.LibVLC;

public sealed class LibVlcPlaybackOptions
{
    public bool EnableDebugLogs { get; init; }

    public bool EnableHardwareDecoding { get; init; } = true;

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}
