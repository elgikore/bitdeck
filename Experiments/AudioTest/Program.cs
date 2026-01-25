// See https://aka.ms/new-console-template for more information


using System.Runtime.InteropServices.ComTypes;
using LibVLCSharp.Shared;

Core.Initialize();
var libVlc = new LibVLC();
var mediaPlayer = new MediaPlayer(libVlc); 

// Reference: \MusicPlayer\BitDeck\Experiments\AudioTest\bin\Debug\net9.0
string audioPath = Path.GetFullPath("../../../../../../input.wav");

Console.WriteLine(audioPath);

using var metadata = new Media(libVlc, audioPath);
await metadata.Parse();
TimeSpan totalTime = TimeSpan.FromMilliseconds(metadata.Duration);

using var media = new Media(libVlc, audioPath);
mediaPlayer.Play(media);


mediaPlayer.TimeChanged += (_, e) =>
{
    TimeSpan elapsedTime = TimeSpan.FromMilliseconds(e.Time);
    Console.Write($"\r{elapsedTime:mm\\:ss}/{totalTime:mm\\:ss}");
};

Console.ReadKey();