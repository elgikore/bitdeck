using LibVLCSharp.Shared;

namespace AudioTest;

public struct MusicData
{
    public string Title { get; init; }
    public string Artist { get; init; }
    public string AlbumName { get; init; }
    public string AlbumArtPath { get; init; }
    public string Year { get; init; }
    public TimeSpan DurationMilliseconds { get; init; }
    public string AudioPath { get; init; }

    public MusicData(string audioPath)
    {
        using var metadata = new Media(VlcCore.LibVlcInstance, audioPath);
        metadata.Parse();
        
    }
}