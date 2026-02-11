﻿// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;


string audioPath = Path.GetFullPath("../../../../../../input2.m4a");

// using var reader = new WaveFileReader(audioPath);


// Console.WriteLine(audioPath);

// var config = new AudioConfig
// {
//     SampleRate = 48000,
//     Channels = 2,
//     BufferSize = 512
// };
//
// OwnaudioNet.Initialize(config);
// OwnaudioNet.Start();

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

// 1. Initialize the engine context.
using var engine = new MiniAudioEngine();

// 2. Define the audio format for playback.
var format = new AudioFormat
{
    Channels = 2,
    SampleRate = 96000,
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

Console.WriteLine($"Playing audio on '{playbackDevice.Info?.Name}'... Press any key to stop.");
Console.ReadKey();

// 8. Stop the device, which also stops the audio stream.
playbackDevice.Stop();











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