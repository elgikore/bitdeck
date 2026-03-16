using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AudioVisualizerTestSecond;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;

namespace BitDeck.Experiments.AudioVisualizerTestSecond;

public class OpenGlRenderer : OpenGlControlBase
{
    private static GL _gl = null!;
    private uint _mainShader;
    private int _iTimeLocation;
    private int _iResolutionLocation;
    private int _iRmsLocation;
    private int _iMidrangeNormAvgLocation;
    
    private readonly Stopwatch _iTime = new();
    public bool IsPlaying { get; private set; }

    public float TotalDurationSeconds { get; set; }

    public float CurrentRmsLevel { get; set; }
    public float CurrentMidrangeAvgNormLevel { get; set; }
    
    private float _prevMidrangeAvgNormLevel;
    private float _prevRmsLevel;
    private int _iTotalDurationLocation;
    private uint _fbo;
    private uint _fbTexture;
    private uint _screenShader;
    private int _screenTexLocation;
    private uint _vao;

    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _gl = GL.GetApi(gl.GetProcAddress);

        Console.WriteLine(Marshal.PtrToStringUTF8((IntPtr)_gl.GetString(StringName.Version)));

        uint vao = 0; // Need because Avalonia doesn't provide a default VAO unlike GLFW
        _gl.GenVertexArrays(1, &vao); 
        _gl.BindVertexArray(vao);
        _vao = vao;

        float[] vertices =
        [
            -1, -1,  0, 0,           // 0
            1, -1,   1, 0,           // 1
            1, 1,    1, 1,           // 2
            -1, 1,   0, 1            // 3
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
            false, 4 * sizeof(float), 0));
        
        GlCheck.Invoke(_gl, () => _gl.EnableVertexAttribArray(1));
        GlCheck.Invoke(_gl, () => _gl.VertexAttribPointer(1, 2, GLEnum.Float, 
            false, 4 * sizeof(float), 2 * sizeof(float)));

        uint fbo = 0;
        _gl.GenFramebuffers(1, &fbo);
        _gl.BindFramebuffer(GLEnum.Framebuffer, fbo);
        _fbo = fbo;

        uint fbTexture = 0;
        _gl.GenTextures(1, &fbTexture);
        _gl.BindTexture(GLEnum.Texture2D, fbTexture);
        _gl.TexImage2D(GLEnum.Texture2D, 0, InternalFormat.Rgb, 256, 256, 0, 
            GLEnum.Rgb, GLEnum.UnsignedByte, null);
        

        #pragma warning disable CS9193 // Argument should be a variable because it is passed to a 'ref readonly' parameter
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        #pragma warning restore CS9193 // Argument should be a variable because it is passed to a 'ref readonly' parameter

        _fbTexture = fbTexture;

        uint rbo = 0;
        _gl.GenRenderbuffers(1, &rbo);
        _gl.BindRenderbuffer(GLEnum.Renderbuffer, rbo);
        _gl.RenderbufferStorage(GLEnum.Renderbuffer, GLEnum.DepthComponent, 256, 256);
        _gl.FramebufferRenderbuffer(GLEnum.Framebuffer, GLEnum.DepthAttachment, GLEnum.Renderbuffer, rbo);
        
        // Absolutely needed or it won't render
        // https://www.opengl-tutorial.org/intermediate-tutorials/tutorial-14-render-to-texture/
        _gl.FramebufferTexture(GLEnum.Framebuffer, GLEnum.ColorAttachment0, fbTexture, 0);

        var fboStatus = _gl.CheckFramebufferStatus(GLEnum.Framebuffer);

        if ((int)fboStatus != (int)GLEnum.FramebufferComplete) throw new GlException($"Framebuffer incomplete! ({fboStatus})");
        Console.WriteLine(fboStatus);
        
        
        
        
        GlCheck.Invoke(_gl, () => _gl.Viewport(0, 0, 256, 256)); // Need because Avalonia doesn't provide it unlike GLFW

        string vertexShader = File.ReadAllText(Path.GetFullPath("../../../Experiments/AudioVisualizerTestSecond/vertexShader.vert"));
        string fragShader = File.ReadAllText(Path.GetFullPath("../../../Experiments/AudioVisualizerTestSecond/mandelbrot.frag"));
        string screenShader = File.ReadAllText(Path.GetFullPath("../../../Experiments/AudioVisualizerTestSecond/screen.frag"));
        
        _mainShader = CreateShader(ref vertexShader, ref fragShader);
        _screenShader = CreateShader(ref vertexShader, ref screenShader);
        
        GlCheck.Invoke(_gl, () => _gl.UseProgram(_mainShader));
        
        // iResolution
        _iResolutionLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iResolution"));
        GlCheck.Invoke(_gl, () => _gl.Uniform2(_iResolutionLocation, 256f, 256f));
        
        // iTime
        _iTimeLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iTime"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTimeLocation, 0f));
        
        // iRMS
        _iRmsLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iRms"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iRmsLocation, 0f));
        
        // iMidrange
        _iMidrangeNormAvgLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iMidrange"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iMidrangeNormAvgLocation, 0f));
        
        // iTotalDuration
        _iTotalDurationLocation = GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_mainShader, "iTotalDuration"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTotalDurationLocation, TotalDurationSeconds));
        
        
        GlCheck.Invoke(_gl, () => _gl.UseProgram(_screenShader));
        _screenTexLocation =  GlCheck.Invoke(_gl, () => _gl.GetUniformLocation(_screenShader, "screenTex"));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_screenTexLocation, 0));
        
        
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        float smoothedRms;
        float smoothMidrangeAvgNorm;
        
        
        _gl.BindFramebuffer(GLEnum.Framebuffer, _fbo);
        _gl.Viewport(0, 0, 256, 256);
        GlCheck.Invoke(_gl, () => _gl.UseProgram(_mainShader));
        _gl.ClearColor(0f, 0f, 1f, 1f);
        GlCheck.Invoke(_gl, () => _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Enable(GLEnum.DepthTest);
        
        
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTotalDurationLocation, TotalDurationSeconds));
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iTimeLocation, (float)_iTime.Elapsed.TotalSeconds)); // Need because it changes over time
        GlCheck.Invoke(_gl, () => _gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null));
        
        if (!IsPlaying && _prevRmsLevel > 1e-4f)
        {
            smoothedRms = Smoothing(_prevRmsLevel, CurrentRmsLevel, 0.05f);
            GlCheck.Invoke(_gl, () => _gl.Uniform1(_iRmsLocation, smoothedRms));
            _prevRmsLevel = smoothedRms;
            
            smoothMidrangeAvgNorm = Smoothing(_prevMidrangeAvgNormLevel, CurrentMidrangeAvgNormLevel, 0.4f);
            GlCheck.Invoke(_gl, () => _gl.Uniform1(_iMidrangeNormAvgLocation, smoothMidrangeAvgNorm));
            _prevMidrangeAvgNormLevel = smoothMidrangeAvgNorm;
            
            Dispatcher.UIThread.Post(RequestNextFrameRendering); // To make it loop
            return;
        }

        if (!IsPlaying && _prevRmsLevel < 1e-4f)
        {
            _prevRmsLevel = 0;
            _prevMidrangeAvgNormLevel = 0;
            _iTime.Reset();
            GlCheck.Invoke(_gl, () => _gl.Clear(ClearBufferMask.ColorBufferBit));
            Dispatcher.UIThread.Post(RequestNextFrameRendering); // To make it loop
            return;
        }
        
        if (!IsPlaying && _prevRmsLevel == 0f) return;
        

        smoothedRms = Smoothing(_prevRmsLevel, CurrentRmsLevel, 0.1f);
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iRmsLocation, smoothedRms));
        _prevRmsLevel = smoothedRms;

        smoothMidrangeAvgNorm = Smoothing(_prevMidrangeAvgNormLevel, CurrentMidrangeAvgNormLevel, 0.35f);
        GlCheck.Invoke(_gl, () => _gl.Uniform1(_iMidrangeNormAvgLocation, smoothMidrangeAvgNorm));
        _prevMidrangeAvgNormLevel = smoothMidrangeAvgNorm;
        
        
        // IMPORTANT! NOT 0! Avalonia has its default ID -- or else total black screen!
        // This is one of the most infuriating bugs I have ever encountered, and it is Avalonia-specific
        _gl.BindFramebuffer(GLEnum.Framebuffer, (uint)fb); 
        
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        _gl.ClearColor(0f, 1f, 0f, 1f);
        GlCheck.Invoke(_gl, () => _gl.Clear(ClearBufferMask.ColorBufferBit));
        
        
        GlCheck.Invoke(_gl, () => _gl.UseProgram(_screenShader));
        _gl.Uniform1(_screenTexLocation, 0f);
        _gl.BindVertexArray(_vao);
        _gl.Disable(GLEnum.DepthTest);
        _gl.ActiveTexture(GLEnum.Texture0);
        _gl.BindTexture(GLEnum.Texture2D, _fbTexture);
        
        GlCheck.Invoke(_gl, () => _gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, null));
        
        Dispatcher.UIThread.Post(RequestNextFrameRendering); // To make it loop
    }

    private float Smoothing(float prevValue, float currValue, float smoothingFactor)
    {
        return ((1 - smoothingFactor) * prevValue) + (smoothingFactor * currValue);
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
        CurrentRmsLevel = 0;
        CurrentMidrangeAvgNormLevel = 0;
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
        _gl.DeleteShader(_screenShader);
        _gl.DeleteTexture(_fbTexture);
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteVertexArray(_vao);
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlLost()
    {
        _iTime.Stop();
        _gl.DeleteShader(_mainShader);
        _gl.DeleteShader(_screenShader);
        _gl.DeleteTexture(_fbTexture);
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteVertexArray(_vao);
        base.OnOpenGlLost();
    }
}