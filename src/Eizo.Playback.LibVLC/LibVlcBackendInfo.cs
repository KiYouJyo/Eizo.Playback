using LibVLCSharp.Shared;

namespace Eizo.Playback.Backends.LibVLC;

public static class LibVlcBackendInfo
{
    public const string Name = "LibVLC";

    public static Version? WrapperAssemblyVersion =>
        typeof(LibVLCSharp.Shared.LibVLC).Assembly.GetName().Version;
}
