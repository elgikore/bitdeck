// See https://aka.ms/new-console-template for more information

// Default initialization (48kHz, stereo, 512 frames)

using Ownaudio.Core;
using OwnaudioNET;
using OwnaudioNET.Sources;

OwnaudioNet.Initialize();

// Custom configuration
var config = new AudioConfig
{
    SampleRate = 44100,
    Channels = 2,
    BufferSize = 256
};
OwnaudioNet.Initialize(config);

string audioPath = Path.GetFullPath("../../../../../input3.mp3");
var source = new FileSource(audioPath);

float[] audioData = source.GetFloatAudioData(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(25));
int nSize = audioData.Length;

// 


Console.WriteLine(audioData.Length);




// Shutdown
OwnaudioNet.Shutdown();
