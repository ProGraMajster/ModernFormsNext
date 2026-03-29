using System;

namespace ModernFormsNext.WindowKit.Backend.MicroCom
{
    /// <summary>
    /// Represents a minimal COM-like interface with lifetime management.
    /// </summary>
    /// <remarks>
    /// This interface mirrors the concept of COM's IUnknown, but simplified for managed interop usage.
    /// </remarks>
    public interface IUnknown : IDisposable
    {
    }
}