namespace NAudioTest;

public record AudioMetadata
{
    public required string FileLocation { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public required string AlbumName { get; init; }
    public required string AlbumArtLocation { get; init; }
    public required string Year { get; init; }
    public required uint SampleRate { get; init; }
    public required uint ChannelCount { get; init; }
    public required TimeSpan DurationSeconds { get; init; }

    // This record class is treated as a Context Pattern
    // No one can create this record other than AudioMetadata.Parse()
    #region PrivateConstructor
    private AudioMetadata(string fileLocation, string title, string artist, string albumName, string albumArtLocation, 
        string year, uint sampleRate, uint channelCount, TimeSpan durationSeconds)
    {
        FileLocation = fileLocation;
        Title = title;
        Artist = artist;
        AlbumName = albumName;
        AlbumArtLocation = albumArtLocation;
        Year = year;
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        DurationSeconds = durationSeconds;
    }
    #endregion 
    
    
}