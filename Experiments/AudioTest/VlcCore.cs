namespace AudioTest;

using LibVLCSharp.Shared;

public sealed class VlcCore
{
    public static readonly LibVLC LibVlcInstance;

    static VlcCore()
    {
        Core.Initialize();
        LibVlcInstance = new LibVLC();
    }
}