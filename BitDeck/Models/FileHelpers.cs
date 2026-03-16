using System;
using System.IO;

namespace BitDeck.Models;

public static class FileHelpers
{
    public static string ResolveToAbsolutePathAndCheck(string fileLocation)
    {
        // If ~
        if (fileLocation.StartsWith('~'))
        {
            string userLocation = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            fileLocation = userLocation + Path.DirectorySeparatorChar + fileLocation[1..];
        }
        
        fileLocation = Path.GetFullPath(fileLocation);
        
        return !File.Exists(fileLocation) ? 
            (Directory.Exists(fileLocation) ? 
                throw new NotSupportedException("This is a directory!") : 
                throw new FileNotFoundException("File not found.", fileLocation) ) : 
            fileLocation;
    }
}