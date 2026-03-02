namespace NAudioTest;

public record SongMetadata
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
}