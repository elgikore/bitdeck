using System.Diagnostics;
using System.Text.Json;

namespace NAudioTest;

public static class FfmpegHelpers
{
    public static JsonElement FfprobeAudio(string fileLocation)
    {
        fileLocation = FileHelpers.ResolveToAbsolutePathAndCheck(fileLocation);
        
        var ffprobeArgs = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{fileLocation}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        
        return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(fileLocation));
    }
}