using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
    // VLC specific
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly MediaPlayer _visualizerMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    
    
    private const int NumOfPoints = 256; // RMS
    private const int NumOfSamples = 512; // Actual waveform
    private const float SignedInt16Normalizer = -1 * short.MinValue;
    private readonly float[] _waveformPoints = new float[NumOfPoints];
    private readonly float[] _waveformPointsNegative = new float[NumOfPoints];
    private readonly int[] _waveformPointsIdxs = Generate.Consecutive(NumOfPoints, first: 1)
                                                .Select(n => (int)n)
                                                .ToArray();

    private bool _isAudible;
    private readonly DataStreamer _livePlot;
    
    
    // Ring buffer setup since it stutters when ALAC is played
    private class FloatBuffer
    {
        public float[] Buffer { get; }
        public int ActualLength { get; set; }
        private const int DefaultLength = 4092;

        public FloatBuffer() { Buffer = new float[DefaultLength]; }
    }
    
    private const int RingSize = 4;
    private readonly FloatBuffer[] _ringBuffer = [new(), new(), new(), new()];
    
    private int _readIndex;
    private int _writeIndex;
    
    
    
    public MainWindow()
    {
        InitializeComponent();
        
        //Initialize VLC
        Core.Initialize();
        _libVlcInstance = new LibVLC();
        _mainMediaPlayer = new MediaPlayer(_libVlcInstance);
        _visualizerMediaPlayer = new MediaPlayer(_libVlcInstance);
        
        // Media and visualizer synchronization
        _mainMediaPlayer.Playing += (_, _) => _visualizerMediaPlayer.Time = _mainMediaPlayer.Time;
        
        _mainMediaPlayer.TimeChanged += (_, e) =>
        {
            if (e.Time > 0) _isAudible = true;
            
            long currentTime = e.Time;
            long visualizerCurrentTime = _visualizerMediaPlayer.Time;
            
            if (Math.Abs(currentTime - visualizerCurrentTime) < 20) return;
            
            _visualizerMediaPlayer.Time = currentTime;
        };

        _mainMediaPlayer.EndReached += (_, _) =>
        {
            _visualizerMediaPlayer.Stop();
            _isAudible = false;
        };
        
        // Visualizer setup
        _visualizerMediaPlayer.SetAudioFormatCallback((ref IntPtr _, ref IntPtr _, ref uint rate,
            ref uint channels) =>
        {
            // IntPtr format according to the LibVLCSharp docs is 4-byte char*, but reading or writing to it crashes
            // because of reading/writing protected memory, even though the C API suggests that you can read or set it
            
            // Later on I learned that the format is signed 16-bit (short) because when I cast the PCM data to float,
            // I get garbage values. Casting it to short makes the waveform sane
            
            rate = 48000;
            channels = 1;
            
            return 0; // return code
        }, _ => { });
        
        _visualizerMediaPlayer.SetAudioCallbacks((_, samples, count, _) =>
        {
            if (!_isAudible) return;
            
            int nextWriteIdx = (_writeIndex + 1) % RingSize;
            
            if (nextWriteIdx == _readIndex) _readIndex = (_readIndex + 1) % RingSize;
            
            unsafe
            {
                short* samplePoints = (short*)samples;
                int samplePointsLength = (int)count;
                var writeBuffer = _ringBuffer[_writeIndex];
                
                if (samplePoints == null || samplePointsLength == 0) return;

                for (int i = 0; i < samplePointsLength; i++)
                {
                    writeBuffer.Buffer[i] = samplePoints[i] / SignedInt16Normalizer;
                }

                writeBuffer.ActualLength = samplePointsLength;
                
                Volatile.Write(ref _writeIndex, nextWriteIdx);
            }
        }, null, null, null, null);

        
        
        // UI Thread
        const int fps = 30;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1f / fps) };
        
        timer.Tick += (_, _) =>
        {
            int writeIdx = Volatile.Read(ref _writeIndex);
            
            if (!_isAudible || _readIndex == writeIdx) return;
            
            CalculateRms();
            
            Dispatcher.UIThread.Post(() =>
            {
                Plot.Plot.Axes.AntiAlias(false);
                Plot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
                RealPlot.Plot.Axes.AntiAlias(false);
                RealPlot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
                
                Plot.Refresh();
                RealPlot.Refresh();
            });
        };
        
        timer.Start();
        
        
        // RMS Plot
        Plot.UseLayoutRounding = true;
        Plot.RenderTransform = new ScaleTransform(1, 1);
        Plot.Plot.Axes.Color(Colors.Transparent);
        Plot.Plot.HideGrid();
        
        Plot.Plot.Axes.AntiAlias(false);
        Plot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
        Plot.UserInputProcessor.IsEnabled = false;

        var scatterPlot = Plot.Plot.Add.ScatterLine(_waveformPointsIdxs, _waveformPoints);
        scatterPlot.ConnectStyle = ConnectStyle.StepHorizontal;
        scatterPlot.LineWidth = 2;
        
        scatterPlot = Plot.Plot.Add.ScatterLine(_waveformPointsIdxs, _waveformPointsNegative);
        scatterPlot.LineWidth = 2;
        scatterPlot.ConnectStyle = ConnectStyle.StepHorizontal;
        
        
        // Actual Waveform
        RealPlot.UseLayoutRounding = true;
        RealPlot.RenderTransform = new ScaleTransform(1, 1);
        RealPlot.Plot.Axes.Color(Colors.Transparent);
        RealPlot.Plot.HideGrid();
        
        RealPlot.Plot.Axes.AntiAlias(false);
        RealPlot.Plot.Axes.SetLimitsY(short.MinValue, short.MaxValue);
        RealPlot.UserInputProcessor.IsEnabled = false;

        _livePlot = RealPlot.Plot.Add.DataStreamer(NumOfSamples);
        _livePlot.AddRange(Enumerable.Repeat(0, NumOfSamples).Select(n => (double)n).ToArray());
        _livePlot.LineWidth = 2;
        _livePlot.ViewScrollLeft();
    }

    private void CalculateRms()
    {
        var readBuffer = _ringBuffer[_readIndex];
        int chunkSize = (int)Math.Ceiling((float)readBuffer.ActualLength / NumOfPoints);

        for (int i = 0; i < NumOfPoints; i++)
        {
            int startIdx = i * chunkSize;
            int endIdx = Math.Min((i + 1) * chunkSize, readBuffer.ActualLength - 1);
            
            float meanSquare = readBuffer.Buffer[startIdx..endIdx]
                .Select(sample => sample * sample)
                .Average();

            _waveformPoints[i] = MathF.Sqrt(meanSquare);
            _waveformPointsNegative[i] = -1 * _waveformPoints[i];
        }
    }

    private void AddRawSampleWaveform()
    {
        var readBuffer = _ringBuffer[_readIndex];
        
        _livePlot.AddRange(readBuffer.Buffer.Select(sample => (double)sample).ToArray());
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
        string audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");
        
        using var media = new Media(_libVlcInstance, audioPath);
        _mainMediaPlayer.Play(media);
        _visualizerMediaPlayer.Play(media);
        Console.WriteLine("Now playing");
    }
}