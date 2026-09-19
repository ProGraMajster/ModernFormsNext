using ModernFormsNext.Diagnostics;
using SkiaSharp;

namespace ModernFormsNext.Rendering.Skia;

/// <summary>Owns one actual shader wrapper and its optional originating diagnostic lease.</summary>
/// <remarks>
/// This value adds no managed wrapper allocation when recording is disabled. Native shader
/// reference counts and byte sizes are deliberately not estimated. SKObject disposal and
/// the recorder lease are idempotent if an ownership scope is copied by internal code.
/// </remarks>
internal readonly struct SkiaShaderScope : IDisposable
{
    private readonly PerformanceResourceToken resource;

    internal SkiaShaderScope(SKShader? shader)
    {
        Shader = shader;
        resource = shader is null ? default : PerformanceRecorder.CaptureResource(
            PerformanceCounterKind.ShadersCreated, PerformanceCounterKind.ShadersDisposed);
    }

    internal SKShader? Shader { get; }

    public void Dispose()
    {
        if (Shader is null)
            return;
        Shader.Dispose();
        resource.Dispose();
    }
}
