namespace Eizo.Playback;

public sealed record PlaybackSource(
    Uri Uri,
    string? DisplayName = null,
    PlaybackNetworkAccess? NetworkAccess = null,
    IPlaybackRandomAccessSource? RandomAccessSource = null)
{
    public static PlaybackSource FromFile(
        string path,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new PlaybackSource(
            new Uri(Path.GetFullPath(path)),
            displayName);
    }

    public static PlaybackSource FromRandomAccess(
        Uri canonicalUri,
        IPlaybackRandomAccessSource randomAccessSource,
        string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(canonicalUri);
        ArgumentNullException.ThrowIfNull(randomAccessSource);

        if (!canonicalUri.IsAbsoluteUri)
            throw new ArgumentException(
                "Playback URI must be absolute.",
                nameof(canonicalUri));

        if (!string.IsNullOrEmpty(canonicalUri.UserInfo))
            throw new ArgumentException(
                "Credentials must not be embedded in the playback URI.",
                nameof(canonicalUri));

        return new PlaybackSource(
            canonicalUri,
            displayName,
            NetworkAccess: null,
            RandomAccessSource: randomAccessSource);
    }

    public static PlaybackSource FromUri(
        Uri uri,
        string? displayName = null,
        PlaybackNetworkAccess? networkAccess = null)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
            throw new ArgumentException(
                "Playback URI must be absolute.",
                nameof(uri));

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException(
                "Credentials must not be embedded in the playback URI. Use PlaybackNetworkAccess instead.",
                nameof(uri));
        }

        return new PlaybackSource(
            uri,
            displayName,
            networkAccess);
    }
}
