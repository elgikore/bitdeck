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
    private const float SignedInt16Normalizer = -1 * short.MinValue; // Normalize short (signed int16) to float
                                                                     // since VLC always sends signed 16-bit
    
    // PCM Player
    private IAudioEngine _audioEngine = AudioEngineFactory.CreateDefault();
    
    // Only render visualizers if the music is actually audible
    private bool _isAudible;
    
    // Channels
    private int _channels;
    
    // Ring buffer setup so that it doesn't stutter when other audio formats that are not WAV or MP3 (e.g. ALAC)
    // is played
    private class FloatBuffer
    {
        public float[] Buffer { get; } = new float[DefaultLength];
        public int ActualLength { get; set; }
        private const int DefaultLength = 6000; // In case of high quality audio
    }
    
    private const int RingSize = 4;
    private int _readIndex;
    private int _writeIndex;
    private readonly FloatBuffer[] _ringBuffer = [new(), new(), new(), new()];

    // Temp buffer for copying and sending to speakers
    private readonly float[] _tempAudioFloatBuffer = new float[10000];
    
    // Waveform view
    private readonly DataStreamer _livePlot;
    private const int NumOfSamplesInView = 1024; 
    private readonly double[] _blankWaveform = Enumerable.Repeat(0, NumOfSamplesInView)
        .Select(n => (double)n).ToArray();
    
    // DB Meters
    private enum DbLabel { Peak, Rms }
    private const int DefaultMeterValue = -70;
    private readonly Bar[] _dBMeterBars =
    [
        new()
        {
            Position = 0, 
            Value = DefaultMeterValue, 
            ValueBase = DefaultMeterValue, 
            FillColor = new Category10().GetColor(0)
        },
        new()
        {
            Position = 0, 
            Value = DefaultMeterValue, 
            ValueBase = DefaultMeterValue, 
            FillColor = new Category10().GetColor(1)
        }
    ];
    
    private readonly double[] _currentDbReading = new double[Enum.GetValues<DbLabel>().Length];
    
    // Spectrum Analyzer
    
    
    
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
            
            var audioFloatBufferAsSpan = _tempAudioFloatBuffer.AsSpan();
            
            unsafe
            {
                short* samplePoints = (short*)samples;
                int samplePointsLength = (int)count * _channels;
                
                if (samplePoints == null || samplePointsLength == 0) return;

                for (int i = 0; i < samplePointsLength; i++)
                {
                    audioFloatBufferAsSpan[i] = samplePoints[i] / SignedInt16Normalizer;
                }
                
                DownmixToMonoForVisualization(audioFloatBufferAsSpan, samplePointsLength);
                
                // Doesn't throw an exception because we set _isAudible first when playing or restarting the song
                _audioEngine.Send(audioFloatBufferAsSpan[..samplePointsLength]);
                
                Volatile.Write(ref _writeIndex, nextWriteIdx);
            }

            

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
                
                if (_readIndex == writeIdx) continue; // Skip
                    
                double peakDb = CalculatePeakDb();
                double rmsDbfs = CalculateRmsDbfs(); 
                    
                // No need for Interlocked because it is a simple push to the graph
                // DataStreamer handles the current values from us
                AddRawSampleWaveform(); 
                
                Interlocked.Exchange(ref _currentDbReading[(int)DbLabel.Peak], peakDb);
                Interlocked.Exchange(ref _currentDbReading[(int)DbLabel.Rms], rmsDbfs);
                    
                int nextReadIndex = (_readIndex + 1) % RingSize;
                    
                Volatile.Write(ref _readIndex, nextReadIndex);
                
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

        _livePlot = RealPlot.Plot.Add.DataStreamer(NumOfSamplesInView);
        _livePlot.AddRange(_blankWaveform);
        _livePlot.LineWidth = 2;
        _livePlot.ViewScrollLeft();
        
        // Spectrum Analyzer
    }

    private void DownmixToMonoForVisualization(Span<float> audioFloatBuffer, int actualLength)
    {
        var writeBuffer = _ringBuffer[_writeIndex];
        var audioBufferActualLength = audioFloatBuffer[..actualLength];
        int monoSampleCount = audioBufferActualLength.Length / _channels;
        
        for (int i = 0; i < monoSampleCount; i++)
        {
            float sum = 0f;
            int originalIdx = _channels * i;
        
            for (int channel = 0; channel < _channels; channel++) sum += audioBufferActualLength[originalIdx + channel];
        
            writeBuffer.Buffer[i] = sum / _channels;
        }
        
        writeBuffer.ActualLength = monoSampleCount;
    }

    private double CalculatePeakDb()
    {
        var writeBuffer = _ringBuffer[_writeIndex];
        var writeBufferAsSpan = writeBuffer.Buffer.AsSpan(0, writeBuffer.ActualLength);
        
        double absMax = 0;
        
        foreach (var sample in writeBufferAsSpan)
        {
            if (Math.Abs(sample) > absMax) absMax = Math.Abs(sample);
        }
        
        return 20 * Math.Log10(absMax);
    }

    private double CalculateRmsDbfs()
    {
        var writeBuffer = _ringBuffer[_writeIndex];
        var writeBufferAsSpan = writeBuffer.Buffer.AsSpan(0, writeBuffer.ActualLength);
        
        double sumOfSquares = 0;
        
        foreach (var sample in writeBufferAsSpan) sumOfSquares += sample * sample;
        
        double rms = Math.Sqrt(sumOfSquares / writeBufferAsSpan.Length);
        
        return 20 * Math.Log10(rms);
    }

    private void AddRawSampleWaveform()
    {
        // Don't ever use LINQ in tight loops or insanely fast callbacks because it creates a big overhead
        // and tons of copies per stage
        
        var writeBuffer = _ringBuffer[_writeIndex];
        var writeBufferAsSpan = writeBuffer.Buffer.AsSpan(0, writeBuffer.ActualLength);   
        
        foreach (var sample in writeBufferAsSpan) _livePlot.Add(sample);
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
                _dBMeterBars[(int)dbLabel].Value = DefaultMeterValue;
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