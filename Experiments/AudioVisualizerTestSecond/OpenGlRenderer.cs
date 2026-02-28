using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;

namespace AudioVisualizerTestSecond;

public class OpenGlRenderer : OpenGlControlBase
{
    private static GL _gl = null!;
    private uint _mainShader;
    private int _iTimeLocation;
    private int _iResolutionLocation;
    private int _iRmsLocation;
    
    private readonly Stopwatch _iTime = new();
    public bool IsPlaying { get; private set; }
    
    public float CurrentRmsLevel { get; set; }
    private float _prevRmsLevel;
    
    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _gl = GL.GetApi(gl.GetProcAddress);

        uint vao = 0; // Need because Avalonia doesn't provide a default VAO unlike GLFW
        _gl.GenVertexArrays(1, &vao); 
        _gl.BindVertexArray(vao);

        float[] vertices =
        [
            -1, -1, // 0
            1, -1,  // 1
            1, 1,   // 2
            -1, 1   // 3
        ];

        uint[] quadIndices =
        [
            0, 1, 2,
            0, 3, 2
        ];

        uint vboId = 0;
        _gl.GenBuffers(1, &vboId);
        _gl.BindBuffer(GLEnum.ArrayBuffer, vboId);

        fixed (float* verticesPtr = vertices) // GC, don't move this address for a moment
        {
            _gl.BufferData(GLEnum.ArrayBuffer, (UIntPtr)(vertices.Length * sizeof(float)), 
                verticesPtr, GLEnum.StaticDraw);
        }
        
        uint iboId = 0;
        _gl.GenBuffers(1, &iboId);
        _gl.BindBuffer(GLEnum.ElementArrayBuffer, iboId);

        fixed (uint* quadIndicesPtr = quadIndices)
        {
            _gl.BufferData(GLEnum.ElementArrayBuffer, (UIntPtr)(quadIndices.Length * sizeof(uint)), 
                quadIndicesPtr, GLEnum.StaticDraw);
        }
        
        
        GlCheck.Invoke(_gl, () => _gl.EnableVertexAttribArray(0));
        GlCheck.Invoke(_gl, () => _gl.VertexAttribPointer(0, 2, GLEnum.Float, 
            false, 2 * sizeof(float), 0));
        GlCheck.Invoke(_gl, () => _gl.Viewport(0, 0, (uint)Bounds.Width, (uint)Bounds.Height)); // Need because Avalonia doesn't provide it unlike GLFW

        string vertexShader = File.ReadAllText(Path.GetFullPath("../../../vertexShader.vert"));
        string fragShader = File.ReadAllText(Path.GetFullPath("../../../fragShader.frag"));
        
        _mainShader = CreateShader(ref vertexShader, ref fragShader);
        GlCheck.Invoke(_gl, () => _gl.UseProgram(_mainShader));
        
        // iResolution
        _iResolutionLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iResolution"));
        GlCheck.Invoke(_gl, () => _gl.Uniform2(_iResolutionLocation, (float)Bounds.Width, (float)Bounds.Height));
        
        // iTime
        _iTimeLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iTime"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTimeLocation, 0f));
        
        // iRMS
        _iRmsLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iRms"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iRmsLocation, 0f));
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!IsPlaying) return;
        
        GlCheck.Invoke(_gl, () => _gl.Clear(ClearBufferMask.ColorBufferBit));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTimeLocation, (float)_iTime.Elapsed.TotalSeconds)); // Need because it changes over time
        GlCheck.Invoke(_gl, () => _gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null));

        float smoothedRms = SmoothedRms(0.05f);
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iRmsLocation, smoothedRms));
        _prevRmsLevel = smoothedRms;
        
        Dispatcher.UIThread.Post(RequestNextFrameRendering); // To make it loop
    }

    private float SmoothedRms(float smoothingFactor)
    {
        return ((1 - smoothingFactor) * _prevRmsLevel) + (smoothingFactor * CurrentRmsLevel);
    }

    public void Start()
    {
        IsPlaying = true;
        _iTime.Start();
        Dispatcher.UIThread.Post(RequestNextFrameRendering);
    }

    public void Stop()
    {
        IsPlaying = false;
        _iTime.Stop();
        Dispatcher.UIThread.Post(RequestNextFrameRendering);
    }
    
    
    private static uint CreateShader(ref string vertexShader, ref string fragShader) 
    {
        uint program = GlCheck.Invoke(_gl, () => _gl.CreateProgram());
        uint vs = CompileShader(ref vertexShader, GLEnum.VertexShader);
        uint fs = CompileShader(ref fragShader, GLEnum.FragmentShader);
        
        _gl.AttachShader(program, vs);
        _gl.AttachShader(program, fs);
        _gl.LinkProgram(program);
        _gl.ValidateProgram(program);
        
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        return program;
    }

    private static unsafe uint CompileShader(ref string source, GLEnum type)
    {
        uint id = GlCheck.Invoke(_gl, () => _gl.CreateShader(type));
        
        // If not for the convenience function, this is the equivalent of const char* in C# and ShaderSource
        // byte[] utf8 = Encoding.UTF8.GetBytes(source);
        // fixed (byte* ptr = utf8)
        // {
        //     byte** list = stackalloc byte*[1];
        //     list[0] = ptr; 
        //     gl.ShaderSource(shader, 1, list, null);
        // }
        _gl.ShaderSource(id, source);
        _gl.CompileShader(id);

        int result = 0;
        _gl.GetShader(id, GLEnum.CompileStatus, &result);

        
        if (result != (int)GLEnum.False) return id;
        
        
        int length = 0;
        _gl.GetShader(id, GLEnum.InfoLogLength, &length);
        uint lengthUint = (uint)length; // need to cast because Silk.NET uses uint instead of int
        byte* messageByte = stackalloc byte[length];
        
        _gl.GetShaderInfoLog(id, (uint)length, &lengthUint, messageByte);

        string whichFailedToCompile = (type == GLEnum.VertexShader) ? "vertex" : "fragment";
        string message = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(messageByte, length));

        Console.WriteLine($"Failed to compile {whichFailedToCompile}!\n{message}");
        _gl.DeleteShader(id);
        return 0;
    }
    
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _iTime.Stop();
        _gl.DeleteShader(_mainShader);
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlLost()
    {
        _iTime.Stop();
        _gl.DeleteShader(_mainShader);
        base.OnOpenGlLost();
    }
}