namespace Eizo.Playback.Backends.LibVLC;

internal static class LibVlcSurfaceOptions
{
    private const string D3DContextPrefix = "--winrt-d3dcontext=";
    private const string SwapChainPrefix = "--winrt-swapchain=";

    public static LibVlcPlaybackOptions Compose(
        LibVlcPlaybackOptions source,
        IReadOnlyList<string> surfaceArguments)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(surfaceArguments);

        if (surfaceArguments.Count == 0)
        {
            throw new ArgumentException(
                "The video surface did not provide LibVLC rendering arguments.",
                nameof(surfaceArguments));
        }

        var arguments = source.Arguments
            .Where(static argument => !IsReservedSurfaceArgument(argument))
            .Concat(surfaceArguments)
            .ToArray();

        return new LibVlcPlaybackOptions
        {
            EnableDebugLogs = source.EnableDebugLogs,
            EnableHardwareDecoding = source.EnableHardwareDecoding,
            Arguments = arguments
        };
    }

    private static bool IsReservedSurfaceArgument(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        return argument.StartsWith(D3DContextPrefix, StringComparison.OrdinalIgnoreCase)
            || argument.StartsWith(SwapChainPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
