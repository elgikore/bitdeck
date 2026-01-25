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
        
        
        
        
        
        DurationMilliseconds = TimeSpan.FromMilliseconds(metadata.Duration);
        
        // Rate and Channels can be directly accessed because we know it is only one track
        // Map to well known names except others
        ChannelName = metadata.Tracks[0].Data.Audio.Channels switch
        {
            1 => "Mono",
            2 => "Stereo",
            3 => "2.1 Surround",
            4 => "Quad",
            5 => "5.0 Surround",
            6 => "5.1 Surround",
            7 => "6.1 Surround",
            8 => "7.1 Surround",
            var val => $"{val} Channel"
        };
        
        SampleRate = metadata.Tracks[0].Data.Audio.Rate switch
        {
            44100 => "44.1 kHz",
            var val => $"{val / 1000} kHz" // Integer division OK because sample rates behave predictably
        };
        
        AudioPath = audioPath;
    }
}