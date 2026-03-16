using System.Reflection;
using BitDeck.Models;
using JetBrains.Annotations;

namespace BitDeck.Tests.Models;

[TestSubject(typeof(FileHelpers))]
public class FileHelpersTest(ITestOutputHelper output)
{
    [Fact]
    public void ResolveToAbsolutePathAndCheck()
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string relativePath = "../../Debug/net9.0/" + Path.GetFileName(assemblyLocation);
        
        Assert.Equal(FileHelpers.ResolveToAbsolutePathAndCheck(relativePath), assemblyLocation);
    }
    
    [Fact]
    public void ResolveToAbsolutePathAndCheck_ThrowsExceptionWhenInvalidPath()
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string relativePath = @"../../Debug/net9.0>\b\0," + Path.GetFileName(assemblyLocation);
        
        Assert.Throws<FileNotFoundException>(() => FileHelpers.ResolveToAbsolutePathAndCheck(relativePath));
    }
    
    [Fact]
    public void ResolveToAbsolutePathAndCheck_ThrowsExceptionWhenFolder()
    {
        Assert.Throws<NotSupportedException>(() => FileHelpers.ResolveToAbsolutePathAndCheck("~/"));
    }
    
    [Fact]
    public void ResolveToAbsolutePathAndCheck_ThrowsExceptionWhenFileNotFound()
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string mockInvalidFile = "../../Debug/net9.0/" + Path.GetFileName(assemblyLocation) + ".txt";
        
        Assert.Throws<FileNotFoundException>(() => FileHelpers.ResolveToAbsolutePathAndCheck(mockInvalidFile));
    }
}