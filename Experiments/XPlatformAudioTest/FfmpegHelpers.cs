using System.Diagnostics;
using System.Text.Json;

namespace XPlatformAudioTest;

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
        
        using var ffprobe = new Process();
        ffprobe.StartInfo = ffprobeArgs;
        ffprobe.Start();
        
        var outputJson = JsonSerializer.Deserialize<JsonElement>(ffprobe.StandardOutput.ReadToEnd());
        
        ffprobe.Kill();
        
        return outputJson;
    }
}