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
float[] magnitudeData = new float[nSize];
float[] realData = new float[nSize / 2];

Array.Copy(audioData, dataToProcess, nSize);

// Window Function
double[] hanningWindow = Window.HannPeriodic(nSize);
float coherentGain = 0;

for (int i = 0; i < nSize; i++)
{
    dataToProcess[i] *= (float)hanningWindow[i];
    
    coherentGain += (float)hanningWindow[i];
    
    toFft[i] = new Complex(dataToProcess[i], 0);
}

coherentGain /= nSize;

Console.WriteLine(coherentGain);

Fourier.Forward(toFft, FourierOptions.Default);

for (int i = 0; i < nSize; i++) magnitudeData[i] = (float)toFft[i].Magnitude / nSize;

realData[0] = magnitudeData[0] / coherentGain;

for (int i = 1; i < nSize / 2; i++) realData[i] = (2 * magnitudeData[i]) / coherentGain;

if (nSize % 2 == 0) realData[^1] = magnitudeData[nSize / 2] / coherentGain;


foreach (var magnitude in magnitudeData)
{
    Console.WriteLine(magnitude);
}




// Shutdown
OwnaudioNet.Shutdown();
