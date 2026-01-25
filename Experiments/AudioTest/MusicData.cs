using System.Text.RegularExpressions;
using LibVLCSharp.Shared;

// ReSharper disable SuggestVarOrType_BuiltInTypes

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
        
        // Album art path
        var albumArtPath = metadata.Meta(MetadataType.ArtworkURL);
        AlbumArtPath = !string.IsNullOrEmpty(albumArtPath) ? albumArtPath : 
            Path.GetFullPath("../../../../../Assets/NoAlbumArt/pexels-hungtran-3699436-gbcamerafilter.png"); 

        // Year
        var dateToParse = metadata.Meta(MetadataType.Date);
        Year = (TryGetYear(dateToParse, out var year) ? year : File.GetLastWriteTime(audioPath).Year.ToString())!;
        
        // Total Time
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
        
        // Sample Rate as Text
        SampleRate = metadata.Tracks[0].Data.Audio.Rate switch
        {
            44100 => "44.1 kHz",
            var val => $"{val / 1000} kHz" // Integer division OK because sample rates behave predictably
        };
        
        // Audio path for reference later
        AudioPath = audioPath;
    }
    
    private static bool TryGetYear(string? dateToParse, out string? year)
    {
        if (string.IsNullOrWhiteSpace(dateToParse))
        {
            year = null;
            return false;
        }
        
        #pragma warning disable SYSLIB1045
        var match = Regex.Match(dateToParse, @"\d{4}");
        #pragma warning restore SYSLIB1045
        
        bool isSuccessful = match.Success;

        year = isSuccessful ? match.Value : null;
        
        return isSuccessful;
    }
}