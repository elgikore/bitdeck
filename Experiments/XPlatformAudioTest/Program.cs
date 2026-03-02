// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;


string audioPath = Path.GetFullPath("../../../../../../lol2.mp3");

var ffmpeg = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "ffmpeg",
        Arguments = $"-i \"{audioPath}\" -c:a pcm_f32le -f f32le -fflags nobuffer -flags low_delay -",
        RedirectStandardOutput = true,
        RedirectStandardInput = true,
        RedirectStandardError = false,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

ffmpeg.Start();

var ffmpegStdout = ffmpeg.StandardOutput.BaseStream;

// 1. Initialize the engine context.
using var engine = new MiniAudioEngine();


// 2. Define the audio format for playback.
var format = new AudioFormat
{
    Channels = 2,
    SampleRate = 48000,
    Format = SampleFormat.F32
};

// 3. Initialize a specific playback device. Passing `null` uses the system default.
using var playbackDevice = engine.InitializePlaybackDevice(null, format);

// 4. Create a SoundPlayer, passing the engine and format context.
// Make sure you replace "path/to/your/audiofile.wav" with the actual path.
using var dataProvider = new RawDataProvider(ffmpegStdout, SampleFormat.F32, format.SampleRate);
var player = new SoundPlayer(engine, format, dataProvider);

// 5. Add the player to the device's master mixer.
playbackDevice.MasterMixer.AddComponent(player);

// 6. Start the device to begin the audio stream.
playbackDevice.Start();

// 7. Start the player.
player.Play();

while (player.State == PlaybackState.Playing)
{
    Console.WriteLine(TimeSpan.FromSeconds(player.Time).ToString(@"mm\:ss\.fff"));
    Task.Delay(1000).Wait();

    if (TimeSpan.FromSeconds(player.Time).ToString(@"mm\:ss\.fff") ==
        TimeSpan.FromSeconds(126.513938).ToString(@"mm\:ss\.fff")) // Assuming we got the time from ffprobe
    {
        try { ffmpeg.StandardInput.Write('q'); }
        catch (IOException) { } // Suppress broken pipe, we safely quit using 'q'.
        finally { ffmpeg.Kill(); }
        
        break;
    }
}

// 8. Stop the device, which also stops the audio stream.
playbackDevice.Stop();

