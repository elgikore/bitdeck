using System.Text.RegularExpressions;
using LibVLCSharp.Shared;

// ReSharper disable SuggestVarOrType_BuiltInTypes

namespace AudioTest;

public record MusicData
{
    public readonly string Title;
    public readonly string Artist;
    public readonly string AlbumName;
    public readonly string AlbumArtPath;
    public readonly string Year;
    public readonly TimeSpan DurationMilliseconds;
    public readonly string SampleRate;
    public readonly string ChannelName;
    public readonly string AudioPath;

    public MusicData(string audioPath)
    {
        using var metadata = new Media(VlcCore.LibVlcInstance, audioPath);
        string fileName = Path.GetFileNameWithoutExtension(audioPath);
        
        metadata.Parse().Wait(); // Block first until finished parsing
        
        // Title
        var title = metadata.Meta(MetadataType.Title); 
        Title = !string.IsNullOrWhiteSpace(title) && title != Path.GetFileName(audioPath) ? title : fileName;
        
        // Artist
        var artist = metadata.Meta(MetadataType.Artist);
        Artist = !string.IsNullOrWhiteSpace(artist) ? artist : "Unknown Artist";
        
        // Album name
        var albumName = metadata.Meta(MetadataType.Album);
        AlbumName = !string.IsNullOrWhiteSpace(albumName) ? albumName : fileName;
        
        // Album art path
        // Source: https://www.pexels.com/photo/close-up-photo-of-jellyfish-3699436/
        var albumArtPath = metadata.Meta(MetadataType.ArtworkURL);
        AlbumArtPath = !string.IsNullOrWhiteSpace(albumArtPath) ? new Uri(albumArtPath).LocalPath : 
            Path.GetFullPath("../../../../../Assets/NoAlbumArt/pexels-hungtran-3699436-gbcamerafilter.png"); 

        // Year
        // Fallback to modified time if the condition is false
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