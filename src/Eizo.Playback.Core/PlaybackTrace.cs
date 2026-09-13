using System.Diagnostics;

namespace Eizo.Playback.Core;

/// <summary>
/// Lightweight diagnostic tracing shared by the playback backend and its UI host.
/// It intentionally has no dependency on an application logging framework.
/// </summary>
public static class PlaybackTrace
{
    public static void Write(
        string component,
        string operation,
        string phase,
        string? detail = null)
    {
        var message =
            $"[{DateTimeOffset.UtcNow:O}] [T{Environment.CurrentManagedThreadId}] " +
            $"{component}/{operation} {phase}";

        if (!string.IsNullOrWhiteSpace(detail))
        {
            message += " | " + detail;
        }

        Trace.WriteLine(message, "Eizo.Playback");
    }
}
