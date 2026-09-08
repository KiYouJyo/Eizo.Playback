using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.WinUI;

internal static class LibVlcWinUiArguments
{
    private const string D3DContextPrefix = "--winrt-d3dcontext=";
    private const string SwapChainPrefix = "--winrt-swapchain=";

    public static LibVlcPlaybackOptions Compose(
        LibVlcPlaybackOptions source,
        IReadOnlyList<string> swapChainOptions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(swapChainOptions);

        if (swapChainOptions.Count == 0)
        {
            throw new ArgumentException(
                "WinUI VideoView did not provide swap-chain options.",
                nameof(swapChainOptions));
        }

        var arguments = source.Arguments
            .Where(static argument => !IsReservedSurfaceArgument(argument))
            .Concat(swapChainOptions)
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
