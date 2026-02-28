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
            string audioPath = Path.GetFullPath("../../../input2Copy.wav");
                
            using var engine = new MiniAudioEngine();
            var audioFormat = new AudioFormat() { Channels = 2, Format = SampleFormat.S16, SampleRate = 96000 };
            
            using var device = engine.InitializePlaybackDevice(null, audioFormat);
            using var dataProvider = new StreamDataProvider(engine, new FileStream(audioPath, FileMode.Open));
            var player = new SoundPlayer(engine, device.Format, dataProvider);
            
            var spectrumAnalyzer = new SpectrumAnalyzer(audioFormat, fftSize: 2048);

            // Attach the analyzer to the player.
            player.AddAnalyzer(spectrumAnalyzer);
            
            device.MasterMixer.AddComponent(player);
            device.Start();
            player.Play();
            
            var timer = new System.Timers.Timer(100f);
            timer.Elapsed += (_, _) =>
            {
                // Get the spectrum data from the analyzer.
                var spectrumData = spectrumAnalyzer.SpectrumData;

                // Print the magnitude of the first few frequency bins.
                if (spectrumData.Length <= 0) return;
                
                float sum = 0;
                int n = 0;
                
                for (int i = 6; i < 65; i++)
                {
                    sum += spectrumData[i];
                    n++;
                }
                
                
                GlRenderer.CurrentRmsLevel = MathF.Floor(sum / n);
            };
            

            while (player.State == PlaybackState.Playing)
            {
                if (!GlRenderer.IsPlaying)
                {
                    GlRenderer.Start();
                    timer.Start();
                }
                
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            
            GlRenderer.Stop();
        });
    }
}