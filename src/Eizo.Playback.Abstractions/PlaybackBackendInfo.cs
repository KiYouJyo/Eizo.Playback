namespace Eizo.Playback;

public sealed record PlaybackBackendInfo(
    string Name,
    string? Version,
    string? Changeset,
    string? Compiler,
    Version? WrapperVersion,
    bool HardwareDecodingRequested,
    bool? HardwareDecodingActive);
