// See https://aka.ms/new-console-template for more information

using System.Collections.Immutable;
using AudioTest;
using LibVLCSharp.Shared;

var libVlc = VlcCore.LibVlcInstance;

var mediaPlayer = new MediaPlayer(libVlc); 

// Reference: \MusicPlayer\BitDeck\Experiments\AudioTest\bin\Debug\net9.0
string audioPath = Path.GetFullPath("../../../../../../input2.m4a");

Console.WriteLine(audioPath);

using var metadata = new Media(libVlc, audioPath);
await metadata.Parse();
TimeSpan totalTime = TimeSpan.FromMilliseconds(metadata.Duration);

Console.WriteLine(metadata.Tracks[0].Data.Audio.Channels);
Console.WriteLine(metadata.Tracks[0].Data.Audio.Rate);

// using var media = new Media(libVlc, audioPath);
// mediaPlayer.Play(media);
//
//
// mediaPlayer.TimeChanged += (_, e) =>
// {
//     TimeSpan elapsedTime = TimeSpan.FromMilliseconds(e.Time);
//     Console.Write($"\r{elapsedTime:mm\\:ss}/{totalTime:mm\\:ss}");
// };
//
// mediaPlayer.EndReached += (_, _) => Environment.Exit(0);
//
// Console.ReadKey(); // Need or else it quits instantly