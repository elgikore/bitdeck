﻿// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Ownaudio.Core;
using Ownaudio.Decoders;
using OwnaudioNET;
using OwnaudioNET.Mixing;
using OwnaudioNET.Sources;

string audioPath = Path.GetFullPath("../../../../../../input2.m4a");

// using var reader = new WaveFileReader(audioPath);


// Console.WriteLine(audioPath);

var config = new AudioConfig
{
    SampleRate = 48000,
    Channels = 2,
    BufferSize = 512
};

OwnaudioNet.Initialize(config);
OwnaudioNet.Start();

var ffmpeg = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "ffmpeg",
        Arguments = $"-i \"{audioPath}\" -c:a pcm_f32le -f f32le -fflags nobuffer -flags low_delay -",
        RedirectStandardOutput = true,
        RedirectStandardError = false,
        UseShellExecute = false,
        CreateNoWindow = true
    }
};

ffmpeg.Start();

var ffmpegStdout = ffmpeg.StandardOutput.BaseStream;













// // var nice = new WaveFileReader(ffmpegStdout);
// //
// // nice.
//
// // OwnaudioNet.
//
// byte[] buffer = new byte[4096 * config.Channels * sizeof(float)];
// int read;
//
// while ((read = ffmpegStdout.Read(buffer, 0, buffer.Length)) > 0)
// {
//     var bufferAsFloat = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, read));
//     OwnaudioNet.Send(bufferAsFloat);
//     
//     await Task.Delay(TimeSpan.FromSeconds((float)read / (config.SampleRate * config.Channels * sizeof(float))));
// }
//
// OwnaudioNet.Shutdown();

// Console.ReadLine();
//
// dyanmicSource.Stop();
// dyanmicSource.Dispose();
//
// OwnaudioNet.Shutdown();

// AudioDecoderFactory.Create()

// audioEngine.Start();
//
// int samplesRead;
//
// while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
// {
//     Console.WriteLine($"Samples read: {samplesRead}");
//     
//     var bufferAsShort = MemoryMarshal.Cast<byte, short>(buffer);
//
//     for (int i = 0; i < bufferAsShort.Length; i++)
//     {
//         bufferAsFloat[i] = bufferAsShort[i] / 32768f;
//     }
//     
//     
//     audioEngine.Send(bufferAsFloat);
// }
//
// audioEngine.Stop();
// audioEngine.Dispose();