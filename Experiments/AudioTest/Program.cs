// See https://aka.ms/new-console-template for more information

using AudioTest;
using LibVLCSharp.Shared;

Console.OutputEncoding = System.Text.Encoding.UTF8; // Enables non-Latin chars

var libVlc = VlcCore.LibVlcInstance;

var mediaPlayer = new MediaPlayer(libVlc); 

// Reference: \MusicPlayer\BitDeck\Experiments\AudioTest\bin\Debug\net9.0
string audioPath = Path.GetFullPath("../../../../../../input2Copy.wav");

Console.WriteLine(audioPath);


var musicData = new MusicData(audioPath);

TimeSpan totalTime = musicData.DurationMilliseconds;
Console.WriteLine(musicData.Title);
Console.WriteLine(musicData.Artist);
Console.WriteLine(musicData.AlbumName);
Console.WriteLine(musicData.AlbumArtPath);
Console.WriteLine(musicData.Year);
Console.WriteLine(musicData.SampleRate);
Console.WriteLine(musicData.ChannelName);

using var media = new Media(libVlc, musicData.AudioPath);
mediaPlayer.Play(media);


mediaPlayer.TimeChanged += (_, e) =>
{
    TimeSpan elapsedTime = TimeSpan.FromMilliseconds(e.Time);
    Console.Write($"\r{elapsedTime:mm\\:ss}/{totalTime:mm\\:ss}");
};

mediaPlayer.EndReached += (_, _) => Environment.Exit(0);

Console.ReadKey(); // Need or else it quits instantly