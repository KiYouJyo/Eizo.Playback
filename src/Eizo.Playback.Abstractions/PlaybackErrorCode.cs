namespace Eizo.Playback;

public enum PlaybackErrorCode
{
    Unknown = 0,
    NoMedia,
    FileNotFound,
    InvalidSource,
    OpenFailed,
    PlayFailed,
    NotSeekable,
    InvalidPosition,
    BackendInitializationFailed,
    BackendFailure
}
