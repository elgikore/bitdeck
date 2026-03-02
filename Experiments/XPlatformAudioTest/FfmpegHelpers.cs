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

        string ffprobeStdout = ffprobe.StandardOutput.ReadToEnd();
        if (ffprobe.ExitCode != 0) throw new Exception($"ffprobe exited with code {ffprobe.ExitCode}!");
        
        var outputJson = JsonSerializer.Deserialize<JsonElement>(ffprobeStdout);
        
        ffprobe.Kill();
        
        return outputJson;
    }
}