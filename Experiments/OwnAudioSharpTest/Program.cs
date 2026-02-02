// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using LibVLCSharp.Shared;
using Ownaudio.Core;
using Ownaudio.Decoders;
using OwnaudioNET;
using OwnaudioNET.Core;
using OwnaudioNET.Mixing;
using OwnaudioNET.Sources;

string audioPath = Path.GetFullPath("../../../../../../input2.m4a");

Core.Initialize();
var libVlcInstance = new LibVLC();
var mainMediaPlayer = new MediaPlayer(libVlcInstance);

float[] samplesToPlay = new float[8192];


// Create and initialize engine
var config = new AudioConfig
{
    SampleRate = 96000,
    Channels = 2,
    BufferSize = 512
};

var engine = AudioEngineFactory.Create(config);

// Start the engine
engine.Start();


mainMediaPlayer.SetAudioFormatCallback((ref IntPtr _, ref IntPtr _, ref uint rate,
    ref uint channels) =>
{
    // IntPtr format according to the LibVLCSharp docs is 4-byte char*, but reading or writing to it crashes
    // because of reading/writing protected memory, even though the C API suggests that you can read or set it
            
    // Later on I learned that the format is signed 16-bit (short) because when I cast the PCM data to float,
    // I get garbage values. Casting it to short makes the waveform sane
            
    // rate = 48000;
    // channels = 2;

    Console.WriteLine(rate);
    Console.WriteLine(channels);
            
    return 0; // return code
}, _ => { });

mainMediaPlayer.SetAudioCallbacks((_, samples, count, pts) =>
{
    unsafe
    {
        short* samplePoints = (short*)samples;
        int samplePointsLength = (int)count * 2; // Can use as is because mono
                
        if (samplePoints == null || samplePointsLength == 0) return;

        for (int i = 0; i < samplePointsLength; i++)
        {
            samplesToPlay[i] = samplePoints[i] / (-1f * short.MinValue);
        }
        
        engine.Send(samplesToPlay.AsSpan(0, samplePointsLength));
        // Console.WriteLine($"Count {count}, Time: {TimeSpan.FromMicroseconds(pts).Milliseconds}");
    }
}, null, null, null, null);

using var media = new Media(libVlcInstance, audioPath);
        
mainMediaPlayer.Play(media);

while (true)
{
    mainMediaPlayer.Time = (long)TimeSpan.FromSeconds(30).TotalMilliseconds;
    await Task.Delay(1000);
}

Console.ReadKey();
// Stop the engine
engine.Stop();
