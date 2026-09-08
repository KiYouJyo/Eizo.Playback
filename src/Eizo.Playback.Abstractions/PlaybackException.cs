namespace Eizo.Playback;

public sealed class PlaybackException : Exception
{
    public PlaybackException(PlaybackErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public PlaybackException(PlaybackErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public PlaybackErrorCode Code { get; }
}
