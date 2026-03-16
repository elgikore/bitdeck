using System.IO;

namespace BitDeck.Models;

public static class FileHelpers
{
    public static string ResolveToAbsolutePathAndCheck(string fileLocation)
    {
        fileLocation = Path.GetFullPath(fileLocation);
        
        return !File.Exists(fileLocation) ? 
            throw new FileNotFoundException("File not found", fileLocation) : 
            fileLocation;
    }
}