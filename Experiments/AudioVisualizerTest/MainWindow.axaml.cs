using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibVLCSharp.Shared;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly MediaPlayer _visualizerMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    private readonly string _audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");
    private const int _numOfPoints = 50;
    private float[] _waveformPoints = new float[_numOfPoints];

    public MainWindow()
    {
        InitializeComponent();
        Core.Initialize();
        
        _libVlcInstance = new LibVLC();

        _mainMediaPlayer = new MediaPlayer(_libVlcInstance);
        _visualizerMediaPlayer = new MediaPlayer(_libVlcInstance);
        
        // Media and visualizer synchronization
        _mainMediaPlayer.Playing += (_, _) => _visualizerMediaPlayer.Time = _mainMediaPlayer.Time;
        
        _mainMediaPlayer.TimeChanged += (_, e) =>
        {
            long currentTime = e.Time;
            long visualizerCurrentTime = _visualizerMediaPlayer.Time;
            
            if (Math.Abs(currentTime - visualizerCurrentTime) < 20) return;
            
            _visualizerMediaPlayer.Time = currentTime;
        };

        _mainMediaPlayer.EndReached += (_, _) => _visualizerMediaPlayer.Stop();
        
        // Visualizer setup
        _visualizerMediaPlayer.SetAudioFormatCallback(
            (ref IntPtr _, ref IntPtr format, ref uint rate, ref uint channels) =>
            {
                format = Marshal.StringToHGlobalAnsi("FL32");
                rate = 48000;
                channels = 1;

                return 0;
            },
            
            opaque =>
            {
                if (opaque == IntPtr.Zero) return;
                
                Marshal.FreeHGlobal(opaque);
            } 
        );
        
        
        
        
        
        double[] values = { 1.2, 3.4, 2.1, 5.0, 4.3 }; 
        Plot.Plot.Axes.Color(Colors.Transparent);
        
        Plot.Plot.HideGrid();
        Plot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            MinimumTickSpacing = 1
        };
        
        Plot.Plot.Axes.AntiAlias(false);
        Plot.UserInputProcessor.IsEnabled = false;
        
        
        var linePlot = Plot.Plot.Add.Signal(values);
        linePlot.MaximumMarkerSize = 0;
        
        Plot.Refresh();
    }

    protected override void OnClosed(EventArgs e)
    {
        _mainMediaPlayer.Dispose();
        _visualizerMediaPlayer.Dispose();
        _libVlcInstance.Dispose();
        
        base.OnClosed(e);
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        using var media = new Media(_libVlcInstance, _audioPath);
        _mainMediaPlayer.Play(media);
        _visualizerMediaPlayer.Play(media);
    }
}