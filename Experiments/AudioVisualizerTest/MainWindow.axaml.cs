using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Ownaudio.Core;
using OwnaudioNET;
using ScottPlot;
using ScottPlot.Palettes;
using ScottPlot.Plottables;
using Colors = ScottPlot.Colors;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    // VLC specific
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    
    // PCM Player
    private IAudioEngine _audioEngine = AudioEngineFactory.CreateDefault();
    
    
    private const int NumOfSamples = 1024; // Actual waveform view
    private const float SignedInt16Normalizer = -1 * short.MinValue;
    private readonly float[] _downmixedMono = new float[5500]; // 5500 samples in case vlc sends a lot of samples

    private bool _isAudible;
    private readonly DataStreamer _livePlot;

    private const int DEFAULT_METER_VALUE = -70;

    private readonly Bar[] _dBMeterBars =
    [
        new()
        {
            Position = 0, 
            Value = DEFAULT_METER_VALUE, 
            ValueBase = DEFAULT_METER_VALUE, 
            FillColor = new Category10().GetColor(0)
        },
        new()
        {
            Position = 0, 
            Value = DEFAULT_METER_VALUE, 
            ValueBase = DEFAULT_METER_VALUE, 
            FillColor = new Category10().GetColor(1)
        }
    ];
    
    private readonly double[] _currentDbReading = new double[Enum.GetValues<DbLabel>().Length];
    
    private enum DbLabel
    {
        Peak,
        Rms
    }
    
    
    // Ring buffer setup since it stutters when ALAC is played
    private class FloatBuffer
    {
        public float[] Buffer { get; } = new float[DefaultLength];
        public int ActualLength { get; set; }
        private const int DefaultLength = 8192; // In case of high quality audio
    }
    
    private const int RingSize = 4;
    private readonly FloatBuffer[] _ringBuffer = [new(), new(), new(), new()];
    
    private int _readIndex;
    private int _writeIndex;
    private int _channels;

    private double[] _blankWaveform = Enumerable.Repeat(0, NumOfSamples).Select(n => (double)n).ToArray();


    public MainWindow()
    {
        InitializeComponent();
        
        //Initialize VLC
        Core.Initialize();
        _libVlcInstance = new LibVLC();
        _mainMediaPlayer = new MediaPlayer(_libVlcInstance);
        
        // Media player
        _mainMediaPlayer.Playing += (_, _) => _isAudible = true;
        
        _mainMediaPlayer.EndReached += (_, _) =>
        {
            _audioEngine.Stop();
            _audioEngine.Dispose();
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

            // Doesn't throw an exception because we set _isAudible first when playing or restarting the song
            _audioEngine.Send(writeBuffer.Buffer.AsSpan(0, writeBuffer.ActualLength));

        }, null, null, null, null);

        // DSP Thread
        Task.Run(() =>
        {
            while (true)
            {
                if (!_isAudible)
                {
                    Thread.Sleep(1); // reduce CPU usage even when continuing
                    continue;
                }
                
                int writeIdx = Volatile.Read(ref _writeIndex);
            
                // Exhaust buffer
                while (_readIndex != writeIdx)
                {
                    DownmixToMonoForVisualization();
                    
                    double peakDb = CalculatePeakDb();
                    double rmsDbfs = CalculateRmsDbfs(); 
                    
                    // No need for Interlocked because it is a simple push to the graph
                    // DataStreamer handles the current values from us
                    AddRawSampleWaveform(); 
                
                    Interlocked.Exchange(ref _currentDbReading[(int)DbLabel.Peak], peakDb);
                    Interlocked.Exchange(ref _currentDbReading[(int)DbLabel.Rms], rmsDbfs);
                    
                    int nextReadIndex = (_readIndex + 1) % RingSize;
                    
                    Volatile.Write(ref _readIndex, nextReadIndex);
                }

                // Console.WriteLine("Written");
                
                Thread.Sleep(1); // sleep for 1 ms to avoid heavy calculations too fast
            }
        });
        
        // UI Thread
        const int fps = 30;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1f / fps) };
        
        timer.Tick += (_, _) =>
        {
            if (!_isAudible) return;
            
            // Get current reading for peak and RMS
            // No need to do this for waveform since a simple DataStreamer push already handles the things for us
            // -- old values are pushed to the left as new values are added to the right
            double peakDb = Interlocked.CompareExchange(ref _currentDbReading[(int)DbLabel.Peak], 0, 0);
            double rmsDbfs = Interlocked.CompareExchange(ref _currentDbReading[(int)DbLabel.Rms], 0, 0);
            
            _dBMeterBars[(int)DbLabel.Peak].Value = peakDb;
            _dBMeterBars[(int)DbLabel.Rms].Value = rmsDbfs;
            
            Dispatcher.UIThread.Post(() =>
            {
                Plot.Plot.Axes.AntiAlias(false);
                Plot.Plot.Axes.SetLimitsY(-0.5, 0.5);
                Plot.Plot.Axes.SetLimitsX(-60, 0.5);
                RealPlot.Plot.Axes.AntiAlias(false);
                RealPlot.Plot.Axes.SetLimitsY(-1, 1);
                
                Plot.Refresh();
                RealPlot.Refresh();
            });
        };
        
        timer.Start();
        
        
        
        // DB Meters
        Plot.UseLayoutRounding = true;
        Plot.RenderTransform = new ScaleTransform(1, 1);
        Plot.Plot.HideGrid();
        
        Plot.Plot.Axes.AntiAlias(false);
        Plot.Plot.Axes.SetLimitsY(-0.5, 0.5);
        Plot.Plot.Axes.SetLimitsX(-60, 0.5);
        Plot.Plot.Axes.Margins(left: 0);
        
        Plot.Plot.Axes.Left.SetTicks(Generate.Consecutive(1), ["dB"]);
        Plot.Plot.Axes.Left.MajorTickStyle.Length = 0;
        Plot.Plot.Axes.Left.TickLabelStyle.FontSize = 14;
        Plot.UserInputProcessor.IsEnabled = false;

        var barPlot = Plot.Plot.Add.Bars(_dBMeterBars);
        barPlot.Horizontal = true;
        
        Plot.Plot.Legend.ManualItems.Add(new LegendItem
            { LabelText = "Peak", FillColor = _dBMeterBars[(int)DbLabel.Peak].FillColor });
        
        Plot.Plot.Legend.ManualItems.Add(new LegendItem
            { LabelText = "RMS", FillColor = _dBMeterBars[(int)DbLabel.Rms].FillColor });
        
        Plot.Plot.ShowLegend(Alignment.UpperRight);
        
        
        // Actual Waveform
        RealPlot.UseLayoutRounding = true;
        RealPlot.RenderTransform = new ScaleTransform(1, 1);
        RealPlot.Plot.Axes.Color(Colors.Transparent);
        RealPlot.Plot.HideGrid();
        
        RealPlot.Plot.Axes.AntiAlias(false);
        RealPlot.Plot.Axes.SetLimitsY(-1, 1);
        RealPlot.UserInputProcessor.IsEnabled = false;

        _livePlot = RealPlot.Plot.Add.DataStreamer(NumOfSamples);
        _livePlot.AddRange(_blankWaveform);
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

            for (int channel = 0; channel < _channels; channel++) sum += readBufferAsSpan[originalIdx + channel];
        
            _downmixedMono[i] = sum / _channels;
        }
    }

    private double CalculatePeakDb()
    {
        var monoActualLength = _ringBuffer[_readIndex].ActualLength / _channels;
        var downmixedMonoAsSpan = _downmixedMono.AsSpan(0, monoActualLength);

        double absMax = 0;

        foreach (var sample in downmixedMonoAsSpan)
        {
            if (Math.Abs(sample) > absMax) absMax = Math.Abs(sample);
        }

        return 20 * Math.Log10(absMax);
    }

    private double CalculateRmsDbfs()
    {
        var monoActualLength = _ringBuffer[_readIndex].ActualLength /  _channels;
        var downmixedMonoAsSpan = _downmixedMono.AsSpan(0, monoActualLength);

        double sumOfSquares = 0;
        
        foreach (var sample in downmixedMonoAsSpan) sumOfSquares += sample * sample;

        double rms = Math.Sqrt(sumOfSquares / downmixedMonoAsSpan.Length);
        
        return 20 * Math.Log10(rms);
    }

    private void AddRawSampleWaveform()
    {
        // Don't ever use LINQ in tight loops or insanely fast callbacks because it creates a big overhead
        // and tons of copies per stage
        
        var monoActualLength = _ringBuffer[_readIndex].ActualLength / _channels;
        var downmixedMonoAsSpan = _downmixedMono.AsSpan(0, monoActualLength);    

        foreach (var sample in downmixedMonoAsSpan) _livePlot.Add(sample);
    }

    protected override void OnClosed(EventArgs e)
    {
        _mainMediaPlayer.Dispose();
        _libVlcInstance.Dispose();
       _audioEngine.Dispose();
        
        base.OnClosed(e);
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        string audioPath = Path.GetFullPath("../../../../../../input3.mp3");
        
        using var mediaMeta = new Media(_libVlcInstance, audioPath);
        mediaMeta.Parse().Wait();

        _channels = (int)mediaMeta.Tracks[0].Data.Audio.Channels;
        
        var config = new AudioConfig
        {
            SampleRate = (int)mediaMeta.Tracks[0].Data.Audio.Rate,
            Channels = _channels,
            BufferSize = 512
        };
        
        if (_isAudible)
        {
            _isAudible = false; // Putting this here disappears the exception entirely
            Thread.Sleep(2);
            
            foreach (var dbLabel in Enum.GetValues<DbLabel>())
            {
                _dBMeterBars[(int)dbLabel].Value = DEFAULT_METER_VALUE;
            }
            
            _livePlot.AddRange(_blankWaveform);
            
            Plot.Refresh();
            RealPlot.Refresh();
            
            _audioEngine.Stop();
            _audioEngine.Dispose();
            _audioEngine = AudioEngineFactory.Create(config);
        }
        

        Console.WriteLine(_audioEngine.Initialize(config));
        Console.WriteLine(_audioEngine.Start());
        
        
        using var media = new Media(_libVlcInstance, audioPath);
        
        _mainMediaPlayer.Play(media);
        
        Console.WriteLine("Now playing");
    }
}