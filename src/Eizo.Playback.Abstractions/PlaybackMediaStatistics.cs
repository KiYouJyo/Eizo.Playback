namespace Eizo.Playback;

public sealed record PlaybackMediaStatistics(
    long ReadBytes,
    long DemuxReadBytes,
    int DemuxCorrupted,
    int DemuxDiscontinuity,
    int DecodedVideo,
    int DecodedAudio,
    int DisplayedPictures,
    int LostPictures,
    int PlayedAudioBuffers,
    int LostAudioBuffers,
    int SentPackets,
    long SentBytes);
