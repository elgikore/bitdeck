using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using Colors = ScottPlot.Colors;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly MediaPlayer _visualizerMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    private readonly string _audioPath = Path.GetFullPath("../../../../../../input.mp3");
    private const int NumOfPoints = 512;
    // private int _currentIndex;
    // private bool _canRedraw;
    // private float[] _waveformPoints = new float[NumOfPoints];
    // private int[] _waveformPointsIdxs = Generate.Consecutive(NumOfPoints, first: 1)
    //                                             .Select(n => (int)n)
    //                                             .ToArray();
    
    private DataStreamer _scatterPlot;
    
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
        _visualizerMediaPlayer.SetAudioFormatCallback((ref IntPtr _, ref IntPtr _, ref uint rate,
            ref uint channels) =>
        {
            // IntPtr format according to the docs is 4-byte char*, but reading or writing to it crashes
            // because of reading/writing protected memory, even though the C API suggests that you can read or set it
            
            // Later on I learned that the format is signed 16-bit (short) because when I cast the PCM data to float,
            // I get garbage values. Casting it to short makes the waveform sane
            
            rate = 48000;
            channels = 1;
            
            return 0; // return code
        }, _ => { });
        
        // _visualizerMediaPlayer.SetAudioFormat("FL32", 48000, 1); doesnt work
        
        _visualizerMediaPlayer.SetAudioCallbacks((opaque, samples, count, _) =>
        {
            unsafe
            {
                short* waveformPoints = (short*)samples;
                int waveformPointsLength = (int)count;
                
                if (waveformPointsLength == 0) return;
                
                for (int i = 0; i < waveformPointsLength; i++) _scatterPlot.Add(waveformPoints[i]);
            }

            // Console.WriteLine($"Min: {pointsCopy.Min()}, Max: {pointsCopy.Max()}");
            // if (!_canRedraw) return;
            
            // Dispatcher.UIThread.Post(() =>
            // {
            //     // Plot.Plot.GetPlottables<Marker>()
            //     //     .Where(m => m.X < 0)
            //     //     .ToList()
            //     //     .ForEach(m => Plot.Plot.Remove(m));
            //     
            //     Plot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
            //     Plot.Refresh();
            // });
            
            
        }, null, null, null, null);

        int fps = 30;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1f / fps) };
        
        timer.Tick += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // Plot.Plot.GetPlottables<Marker>()
                //     .Where(m => m.X < 0)
                //     .ToList()
                //     .ForEach(m => Plot.Plot.Remove(m));
                
                Plot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
                Plot.Refresh();
            });
        };
        
        timer.Start();
        
        Plot.UseLayoutRounding = true;
        Plot.RenderTransform = new ScaleTransform(1, 1);

        Plot.Plot.Axes.Color(Colors.Transparent);
        
        Plot.Plot.HideGrid();
        Plot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            IntegerTicksOnly = true,
            MinimumTickSpacing = 1
        };
        
        Plot.Plot.Axes.AntiAlias(false);
        Plot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
        Plot.UserInputProcessor.IsEnabled = false;

        _scatterPlot = Plot.Plot.Add.DataStreamer(NumOfPoints);
        _scatterPlot.ViewScrollLeft();
        _scatterPlot.LineWidth = 0;
        
        _scatterPlot.MarkerSize = 5;
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