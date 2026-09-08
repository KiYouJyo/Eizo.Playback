using LibVLCSharp.Shared;

namespace Eizo.Playback.LibVLC;

public static class LibVlcBackendInfo
{
    public const string Name = "LibVLC";

    public static Version? WrapperAssemblyVersion =>
        typeof(LibVLC).Assembly.GetName().Version;
}
