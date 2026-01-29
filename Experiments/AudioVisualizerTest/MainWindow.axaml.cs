using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LibVLCSharp.Shared;

namespace AudioVisualizerTest;

public partial class MainWindow : Window
{
    private readonly MediaPlayer _mainMediaPlayer;
    private readonly MediaPlayer _visualizerMediaPlayer;
    private readonly LibVLC _libVlcInstance;
    private readonly string _audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");

    public MainWindow()
    {
        InitializeComponent();
        Core.Initialize();
        
        _libVlcInstance = new LibVLC();

        _mainMediaPlayer = new MediaPlayer(_libVlcInstance);
        _visualizerMediaPlayer = new MediaPlayer(_libVlcInstance);
        
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
        _visualizerMediaPlayer.Play(media);
    }
}