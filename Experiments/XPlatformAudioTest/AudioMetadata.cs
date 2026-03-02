using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XPlatformAudioTest;

public record AudioMetadata
{
    public required string FileLocation { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string AlbumName { get; init; }
    public required string? AlbumArtLocation { get; init; }
    public required uint Year { get; init; }
    public required uint SampleRate { get; init; }
    public required uint ChannelCount { get; init; }
    public required TimeSpan DurationSeconds { get; init; }

    // This record class is treated as a Context Pattern
    // No one can create this record other than AudioMetadata.Parse()
    #region PrivateConstructor
    private AudioMetadata() { }
    
    #endregion

    [SuppressMessage("ReSharper", "JoinDeclarationAndInitializer")]
    public static AudioMetadata Parse(string fileLocation)
    {
        fileLocation = FileHelpers.ResolveToAbsolutePathAndCheck(fileLocation);
        
        // Defaults
        string title = Path.GetFileNameWithoutExtension(fileLocation);
        string artist = "Unknown Artist";
        string albumName = Path.GetFileNameWithoutExtension(fileLocation);
        string? albumArtLocation = null;
        uint year = (uint)File.GetLastWriteTime(fileLocation).Year;
        uint sampleRate;
        uint channelCount;
        TimeSpan durationSeconds;
        
        // Parse
        var outputFfprobeJson = FfmpegHelpers.FfprobeAudio(fileLocation);
        
        // Main tree
        if (!outputFfprobeJson.TryGetProperty("format", out var format) ||
            !outputFfprobeJson.TryGetProperty("streams", out var streams))
        {
            throw new InvalidDataException("Could not parse audio file metadata! Might not be a proper ffprobe JSON " +
                            "output, an audio corruption, or an unusual codec.");
        }
        
        var primaryStream = streams[0];
        
        // Tags
        if (format.TryGetProperty("tags", out var tags))
        {
            if (tags.TryGetProperty("title", out var songTitle)) title = songTitle.GetString()!;
            if (tags.TryGetProperty("artist", out var songArtist)) artist = songArtist.GetString()!;
            if (tags.TryGetProperty("album", out var songAlbum)) albumName = songAlbum.GetString()!;
            if (tags.TryGetProperty("date", out var songDate))
            {
                // Try parsing as year only
                if (DateTime.TryParseExact(songDate.GetString()!, "yyyy", CultureInfo.InvariantCulture, 
                        DateTimeStyles.None, out var dt))
                {
                    year = (uint)dt.Year;
                } 
                
                // If not, treat it as a full datetime format
                else if (DateTime.TryParse(songDate.GetString()!, out dt)) year = (uint)dt.Year;
                
                // else do nothing and base the year on last modified time
            }
        }
        
        // Primary stream
        // This app will support popular major audio files only, so treat them as assumed
        // There shouldn't be any errors here because there is a guard for the "stream" variable
        channelCount = primaryStream.GetProperty("channels").GetUInt32();
        sampleRate = primaryStream.GetProperty("sampleRate").GetUInt32();
        durationSeconds = TimeSpan.FromSeconds(double.Parse(primaryStream.GetProperty("duration").GetString()!));
        
        // Album art extraction
        
        return new AudioMetadata()
        {
            FileLocation = fileLocation,
            Title = title,
            Artist = artist,
            AlbumName = albumName,
            AlbumArtLocation = albumArtLocation,
            Year = year,
            SampleRate = sampleRate,
            ChannelCount = channelCount,
            DurationSeconds = durationSeconds
        };
    }
}