namespace AudioTest;

using LibVLCSharp.Shared;



public sealed class VlcSingleton
{
    private static VlcSingleton? _singletonInstance;
    private static readonly LibVLC LibVlc = new();
    
    static VlcSingleton() { }
    private VlcSingleton() { }

    public static LibVLC LibVlcInstance
    {
        get
        {
            if (_singletonInstance == null)
            {
                Core.Initialize();
                _singletonInstance = new VlcSingleton();
            }
            
            return LibVlc;
        }
    }
}