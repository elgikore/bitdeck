// See https://aka.ms/new-console-template for more information

// Default initialization (48kHz, stereo, 512 frames)

using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
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

float[] audioData = source.GetFloatAudioData(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(30));
int nSize = audioData.Length;

float[] dataToProcess = new float[nSize];
Complex[] toFft = new Complex[nSize];

Array.Copy(audioData, dataToProcess, nSize);

// Window Function
double[] hanningWindow = Window.HannPeriodic(nSize);

for (int i = 0; i < nSize; i++)
{
    dataToProcess[i] *= (float)hanningWindow[i];
    
    toFft[i] = new Complex(dataToProcess[i], 0);
}

Fourier.Forward(toFft, FourierOptions.Default);




Console.WriteLine(toFft.Length);

foreach (var bin in toFft)
{
    Console.WriteLine(Complex.Abs(bin));
}




// Shutdown
OwnaudioNet.Shutdown();
