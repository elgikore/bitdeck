using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Silk.NET.OpenGL;

namespace AudioVisualizerTestSecond;

public static class GlCheck
{
    private static readonly int LambdaStringLength = "() => ".Length;
    private static readonly StringBuilder Sb = new();
    
    public static void Invoke(GL glContext, Action glAction, 
        [CallerArgumentExpression("glAction")] string expression = "", 
        [CallerFilePath] string filePath = "", 
        [CallerLineNumber] int lineNumber = 0)
    {
        ClearError(glContext);
        glAction();

        while (true)
        {
            var err = glContext.GetError();
            
            if (err == GLEnum.NoError) break;
            
            Sb.Append($"[{err.ToString()}] Error occured at {expression.AsSpan(LambdaStringLength)}")
                .AppendLine($" on {Path.GetFileName(filePath)}:{lineNumber}.")
                .Append($"    at {filePath}");
            
            throw new GlException(Sb.ToString());
        }
    }
    
    public static T Invoke<T>(GL glContext, Func<T> glAction, 
        [CallerArgumentExpression("glAction")] string expression = "", 
        [CallerFilePath] string filePath = "", 
        [CallerLineNumber] int lineNumber = 0)
    {
        ClearError(glContext);
        var result = glAction();

        while (true)
        {
            var err = glContext.GetError();
            
            if (err == GLEnum.NoError) return result;
            
            Sb.Append($"[{err.ToString()}] Error occured at {expression.AsSpan(LambdaStringLength)}")
                .AppendLine($" on {Path.GetFileName(filePath)}:{lineNumber}.")
                .Append($"    at {filePath}");
            
            throw new GlException(Sb.ToString());
        }
    }

    private static void ClearError(GL glContext)
    {
        while (glContext.GetError() != GLEnum.NoError);
    }
}

public class GlException(string message) : Exception(message)
{
    public override string ToString() { return Message; }
}