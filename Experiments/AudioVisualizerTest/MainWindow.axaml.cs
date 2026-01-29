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
        
        // _visualizerMediaPlayer.SetAudioFormatCallback();
    }
    
    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        using var media = new Media(_libVlcInstance, _audioPath);
        _visualizerMediaPlayer.Play(media);
    }
}