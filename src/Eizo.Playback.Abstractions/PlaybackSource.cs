namespace Eizo.Playback;

public sealed record PlaybackSource(Uri Uri, string? DisplayName = null)
{
    public static PlaybackSource FromFile(string path, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PlaybackSource(new Uri(Path.GetFullPath(path)), displayName);
    }
}
