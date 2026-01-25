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
    public string SampleRate { get; init; }
    public string ChannelName { get; init; }
    public string AudioPath { get; init; }

    public MusicData(string audioPath)
    {
        using var metadata = new Media(VlcCore.LibVlcInstance, audioPath);
        metadata.Parse();
        
    }
    
    // return channelName switch
    // {
    //     ChannelName.Mono => "Mono",
    //     ChannelName.Stereo => "Stereo",
    //     ChannelName.Surround2_1 => "2.1 Surround",
    //     ChannelName.Quad => "Quad",
    //     ChannelName.Surround5_0 => "5.0 Surround",
    //     ChannelName.Surround5_1 => "5.1 Surround",
    //     ChannelName.Surround6_1 => "6.1 Surround",
    //     ChannelName.Surround7_1 => "7.1 Surround",
    //     _ => throw new ArgumentOutOfRangeException(nameof(channelName), channelName, "Invalid channel name!")
    // };
}