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
            string audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");
                
            using var engine = new MiniAudioEngine();
            var audioFormat = new AudioFormat() { Channels = 2, Format = SampleFormat.S16, SampleRate = 96000 };
            
            using var device = engine.InitializePlaybackDevice(null, audioFormat);
            using var dataProvider = new StreamDataProvider(engine, new FileStream(audioPath, FileMode.Open));
            var player = new SoundPlayer(engine, device.Format, dataProvider);

            var levelAnalyzer = new LevelMeterAnalyzer(audioFormat);

            // Attach the analyzer to the player.
            player.AddAnalyzer(levelAnalyzer);
            
            device.MasterMixer.AddComponent(player);
            device.Start();
            player.Play();
            
            var timer = new System.Timers.Timer(100f);
            timer.Elapsed += (_, _) =>
            {
                GlRenderer.CurrentRmsLevel = levelAnalyzer.Rms;
                Console.WriteLine(GlRenderer.CurrentRmsLevel);
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