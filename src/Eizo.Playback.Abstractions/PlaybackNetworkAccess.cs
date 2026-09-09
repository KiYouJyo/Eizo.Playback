namespace Eizo.Playback;

/// <summary>
/// Backend-neutral credentials for authenticated network media.
/// Secrets are intentionally not included in ToString().
/// </summary>
public sealed class PlaybackNetworkAccess
{
    public PlaybackNetworkAccess(
        string? userName,
        string? password)
    {
        if (ContainsLineBreak(userName) || ContainsLineBreak(password))
        {
            throw new ArgumentException(
                "Network credentials cannot contain CR/LF characters.");
        }

        UserName = string.IsNullOrWhiteSpace(userName)
            ? null
            : userName;
        Password = password;
    }

    public string? UserName { get; }

    public string? Password { get; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(UserName) ||
        !string.IsNullOrEmpty(Password);

    public override string ToString() =>
        HasCredentials
            ? $"PlaybackNetworkAccess(UserName={UserName ?? "<none>"}, Password=<redacted>)"
            : "PlaybackNetworkAccess(None)";

    private static bool ContainsLineBreak(string? value) =>
        value?.IndexOfAny(['\r', '\n']) >= 0;
}
