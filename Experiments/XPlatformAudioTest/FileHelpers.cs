namespace NAudioTest;

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