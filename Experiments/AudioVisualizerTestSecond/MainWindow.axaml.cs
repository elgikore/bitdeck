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
            string audioPath = Path.GetFullPath("../../../../../../input2CopyShort.wav");
                
            using var engine = new MiniAudioEngine();
            var audioFormat = new AudioFormat() { Channels = 2, Format = SampleFormat.S16, SampleRate = 96000 };
            
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
            const float endHz = 1000;
            int binStartIdx = (int)MathF.Floor((startHz * fftSize) / audioFormat.SampleRate);
            int binEndIdx = (int)MathF.Floor((endHz * fftSize) / audioFormat.SampleRate);
            int binLength = binEndIdx - binStartIdx + 1;
            
            
            var timer = new System.Timers.Timer(100f);
            timer.Elapsed += (_, _) =>
            {
                // Get the spectrum data from the analyzer.
                var spectrumData = spectrumAnalyzer.SpectrumData.AsSpan(binStartIdx, binLength);

                // Print the magnitude of the first few frequency bins.
                if (spectrumData.Length > 0)
                {
                    float sum = 0;
                    
                    foreach (var t in spectrumData) sum += t;

                    float average = sum / binLength;
                    float normalizedMag = average / sum;
                    
                    GlRenderer.CurrentMidrangeAvgNormLevel = normalizedMag;
                }
                
                
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