using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Ownaudio.Core;
using ScottPlot;
using ScottPlot.Plottables;
using Colors = ScottPlot.Colors;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    // VLC specific
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    
    // PCM Player
    private IAudioEngine? _audioEngine;
    
    
    private const int NumOfPoints = 256; // RMS
    private const int NumOfSamples = 512; // Actual waveform
    private const float SignedInt16Normalizer = -1 * short.MinValue;
    private readonly float[] _waveformPoints = new float[NumOfPoints];
    private readonly float[] _waveformPointsNegative = new float[NumOfPoints];
    private readonly int[] _waveformPointsIdxs = Generate.Consecutive(NumOfPoints, first: 1)
                                                .Select(n => (int)n)
                                                .ToArray();
    
    private readonly float[] _downmixedMono = new float[5500]; // 5500 samples in case vlc sends a lot of samples

    private bool _isAudible;
    private readonly DataStreamer _livePlot;
    private int _rmsGraphXLimit;
    
    
    // Ring buffer setup since it stutters when ALAC is played
    private class FloatBuffer
    {
        public float[] Buffer { get; } = new float[DefaultLength];
        public int ActualLength { get; set; }
        private const int DefaultLength = 8192;
    }
    
    private const int RingSize = 4;
    private readonly FloatBuffer[] _ringBuffer = [new(), new(), new(), new()];
    
    private int _readIndex;
    private int _writeIndex;
    private int _channels;


    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize RMS graph limit
        _rmsGraphXLimit = _waveformPoints.Length;
        
        //Initialize VLC
        Core.Initialize();
        _libVlcInstance = new LibVLC();
        _mainMediaPlayer = new MediaPlayer(_libVlcInstance);
        
        // Media player
        _mainMediaPlayer.Playing += (_, _) => _isAudible = true;
        
        _mainMediaPlayer.EndReached += (_, _) =>
        {
            _audioEngine?.Stop();
            _isAudible = false;
        };
        
        _mainMediaPlayer.SetAudioFormatCallback(
            (ref IntPtr _, ref IntPtr _, ref uint _, ref uint _) => 0, // Use format as is (return code 0)
            _ => { });
        
        _mainMediaPlayer.SetAudioCallbacks((_, samples, count, _) =>
        {
            if (!_isAudible) return;
            
            int nextWriteIdx = (_writeIndex + 1) % RingSize;
            
            // Overwrite when ring buffer is full, no if check needed
            // writeIndex is always one step ahead of readIndex
            
            var writeBuffer = _ringBuffer[_writeIndex];
            
            unsafe
            {
                short* samplePoints = (short*)samples;
                int samplePointsLength = (int)count * _channels; // Can use as is because mono
                
                if (samplePoints == null || samplePointsLength == 0) return;

                for (int i = 0; i < samplePointsLength; i++)
                {
                    writeBuffer.Buffer[i] = samplePoints[i] / SignedInt16Normalizer;
                }
                
                writeBuffer.ActualLength = samplePointsLength;
                Volatile.Write(ref _writeIndex, nextWriteIdx);
            }

            _audioEngine?.Send(writeBuffer.Buffer.AsSpan(0, writeBuffer.ActualLength));
        }, null, null, null, null);

        
        
        // UI Thread
        const int fps = 30;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1f / fps) };
        
        timer.Tick += (_, _) =>
        {
            if (!_isAudible) return;
            
            int writeIdx = Volatile.Read(ref _writeIndex);
            
            // Exhaust buffer
            while (_readIndex != writeIdx)
            {
                DownmixToMonoForVisualization();
                CalculateRms(); 
                AddRawSampleWaveform(); 
                
                int nextReadIndex = (_readIndex + 1) % RingSize;
                Volatile.Write(ref _readIndex, nextReadIndex);
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                Plot.Plot.Axes.AntiAlias(false);
                Plot.Plot.Axes.SetLimitsY(-1, 1);
                Plot.Plot.Axes.SetLimitsX(1, _rmsGraphXLimit);
                RealPlot.Plot.Axes.AntiAlias(false);
                RealPlot.Plot.Axes.SetLimitsY(-1, 1);
                
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
        Plot.Plot.Axes.SetLimitsY(-1, 1);
        Plot.Plot.Axes.SetLimitsX(1, _rmsGraphXLimit);
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
        RealPlot.Plot.Axes.SetLimitsY(-1, 1);
        RealPlot.UserInputProcessor.IsEnabled = false;

        _livePlot = RealPlot.Plot.Add.DataStreamer(NumOfSamples);
        _livePlot.AddRange(Enumerable.Repeat(0, NumOfSamples).Select(n => (double)n).ToArray());
        _livePlot.LineWidth = 2;
        _livePlot.ViewScrollLeft();
    }

    private void DownmixToMonoForVisualization()
    {
        var readBuffer = _ringBuffer[_readIndex];
        var readBufferAsSpan = readBuffer.Buffer.AsSpan(0, readBuffer.ActualLength);
        int sampleCount = readBufferAsSpan.Length / _channels;
        
        for (int i = 0; i < sampleCount; i++)
        {
            float sum = 0f;
            int originalIdx = _channels * i;

            for (int channel = 0; channel < _channels; channel++)
            {
                // Console.WriteLine(readBufferAsSpan[originalIdx + channel]);
                sum += readBufferAsSpan[originalIdx + channel];
            }
        
            _downmixedMono[i] = sum / _channels;
        }
    }

    private void CalculateRms()
    {
        var monoActualLength = _ringBuffer[_readIndex].ActualLength /  _channels;
        
        var downmixedMonoAsSpan = _downmixedMono.AsSpan(0, monoActualLength);
        int chunkSize = (int)Math.Ceiling((float)downmixedMonoAsSpan.Length/ NumOfPoints);
        bool isStartIdxMoreThanBufferLength = false;
        

        for (int i = 0; i < NumOfPoints; i++)
        {
            int startIdx = i * chunkSize;
            int endIdx = Math.Min((i + 1) * chunkSize, downmixedMonoAsSpan.Length - 1);

            // Don't produce slices once the startIdx is larger or equal to readBuffer length
            // Prevents NaNs that crash the whole UI
            if (startIdx >= downmixedMonoAsSpan.Length)
            {
                if (!isStartIdxMoreThanBufferLength)
                {
                    isStartIdxMoreThanBufferLength = true;
                    _rmsGraphXLimit = i + 1; // Use the current index to clamp since i is from NumOfPoint
                }
                
                _waveformPoints[i] = 0; 
                _waveformPointsNegative[i] = 0; 
                continue;
            }
            
            var slice = downmixedMonoAsSpan[startIdx..endIdx];
            
            float sumOfSquares = 0;
            
            foreach (var sample in slice) sumOfSquares += sample * sample;

            _waveformPoints[i] = MathF.Sqrt(sumOfSquares / slice.Length);
            _waveformPointsNegative[i] = -1 * _waveformPoints[i];
        }
    }

    private void AddRawSampleWaveform()
    {
        // Don't ever use LINQ in tight loops or insanely fast callbacks because it creates a big overhead
        // and tons of copies per stage
        
        var monoActualLength = _ringBuffer[_readIndex].ActualLength /  _channels;
        
        var downmixedMonoAsSpan = _downmixedMono.AsSpan(0, monoActualLength);    

        foreach (var sample in downmixedMonoAsSpan) _livePlot.Add(sample);
    }

    protected override void OnClosed(EventArgs e)
    {
        _mainMediaPlayer.Dispose();
        _libVlcInstance.Dispose();
       _audioEngine?.Dispose();
        
        base.OnClosed(e);
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        string audioPath = Path.GetFullPath("../../../../../../input2.m4a");
        
        using var mediaMeta = new Media(_libVlcInstance, audioPath);
        mediaMeta.Parse().Wait();

        _channels = (int)mediaMeta.Tracks[0].Data.Audio.Channels;
        
        var config = new AudioConfig
        {
            SampleRate = (int)mediaMeta.Tracks[0].Data.Audio.Rate,
            Channels = _channels,
            BufferSize = 512
        };
        
        _audioEngine = AudioEngineFactory.Create(config);
        _audioEngine.Start();
        
        using var media = new Media(_libVlcInstance, audioPath);
        
        _mainMediaPlayer.Play(media);
        
        Console.WriteLine("Now playing");
    }
}