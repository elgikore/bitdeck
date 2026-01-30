using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly MediaPlayer _visualizerMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    private readonly string _audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");
    private const int NumOfPoints = 16384;
    private float[] _waveformPoints = new float[NumOfPoints];
    private int[] _waveformPointsIdxs = Generate.Consecutive(NumOfPoints, first: 1)
                                                .Select(n => (int)n)
                                                .ToArray();
    
    private Scatter _scatterPlot;
    
    // private const int FloatSize = sizeof(float);

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
        _visualizerMediaPlayer.SetAudioFormat("FL32", 48000, 1);
        
        _visualizerMediaPlayer.SetAudioCallbacks((_, samples, count, _) =>
        {
            var pointsCopy = new float[NumOfPoints];
            
            unsafe
            {
                short* waveformPoints = (short*)samples;
                int waveformPointsLength = (int)count;
                
                if (waveformPointsLength == 0) return;
                
                for (int i = 0; i < NumOfPoints; i++)
                {
                    int downsampleIdx = (int)(i / (float)NumOfPoints * waveformPointsLength);
                    downsampleIdx = Math.Clamp(downsampleIdx, 0, waveformPointsLength - 1);
                    pointsCopy[i] = waveformPoints[downsampleIdx];
                }
            }

            Console.WriteLine($"Min: {pointsCopy.Min()}, Max: {pointsCopy.Max()}");
            
            Dispatcher.UIThread.Post(() =>
            {
                Array.Copy(pointsCopy, _waveformPoints,  NumOfPoints);
                Plot.Plot.Axes.AutoScaleY();
                Plot.Refresh();
            });
        }, null, null, null, null);
        
        
        Plot.Plot.Axes.Color(Colors.Transparent);
        
        // Plot.Plot.HideGrid();
        Plot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            MinimumTickSpacing = 1
        };
        
        Plot.Plot.Axes.AntiAlias(false);
        Plot.Plot.Axes.SetLimitsY(float.MinValue, float.MaxValue);
        Plot.UserInputProcessor.IsEnabled = false;

        _scatterPlot = Plot.Plot.Add.ScatterLine(_waveformPointsIdxs, _waveformPoints);
        
        _scatterPlot.MarkerSize = 4;
        _scatterPlot.MarkerShape = MarkerShape.FilledSquare;
        
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
        Console.WriteLine("Now playing");
    }
}