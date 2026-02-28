using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using SoundFlow.Visualization;

namespace AudioVisualizerTestSecond;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            string audioPath = Path.GetFullPath("../../../../../../lol2.mp3");
                
            using var engine = new MiniAudioEngine();
            var audioFormat = new AudioFormat() { Channels = 2, Format = SampleFormat.S16, SampleRate = 48000 };
            
            using var device = engine.InitializePlaybackDevice(null, audioFormat);
            using var dataProvider = new StreamDataProvider(engine, new FileStream(audioPath, FileMode.Open));
            var player = new SoundPlayer(engine, device.Format, dataProvider);

            var levelAnalyzer = new LevelMeterAnalyzer(audioFormat);

            int fftSize = 2048;
            var spectrumAnalyzer = new SpectrumAnalyzer(audioFormat, fftSize);
            
            player.AddAnalyzer(spectrumAnalyzer);

            // Attach the analyzer to the player.
            player.AddAnalyzer(levelAnalyzer);
            
            device.MasterMixer.AddComponent(player);
            device.Start();
            player.Play();

            const float startHz = 300;
            const float endHz = 4000;
            int binStartIdx = (int)MathF.Floor((startHz * fftSize) / audioFormat.SampleRate);
            int binEndIdx = (int)MathF.Floor((endHz * fftSize) / audioFormat.SampleRate);
            int binLength = binEndIdx - binStartIdx + 1;
            
            var timer = new System.Timers.Timer(TimeSpan.FromMilliseconds(100));
            timer.Elapsed += (_, _) =>
            {
                // Get the spectrum data from the analyzer.
                if (spectrumAnalyzer.SpectrumData.Length == 0) return;
                if (spectrumAnalyzer.SpectrumData.Length < binLength) return;
                
                var spectrumData = spectrumAnalyzer.SpectrumData.AsSpan(binStartIdx, 
                    binLength);
                
                float sum = 0;
                float max = 0;

                foreach (var t in spectrumData)
                {
                    if (t > max) max = t;
                        
                    sum += t;
                }

                float average = sum / binLength;
                float normalizedMag = average / max;

                // Console.WriteLine(normalizedMag);
                
                if (float.IsNaN(normalizedMag)) return;
                    
                const float noiseGateMidrange = 0.055f; // Stop very small fluctuations
                GlRenderer.CurrentMidrangeAvgNormLevel = (normalizedMag <= noiseGateMidrange) ? 0 : normalizedMag;
                GlRenderer.CurrentRmsLevel = levelAnalyzer.Rms;
            };
            

            while (player.State == PlaybackState.Playing)
            {
                if (!GlRenderer.IsPlaying)
                {
                    GlRenderer.Start();
                    timer.Start();
                }
                
                Thread.Sleep(100);
            }
            
            GlRenderer.Stop();
            timer.Stop();
        });
    }
}