using ModernFormsNext.WindowKit.Metadata;

namespace ModernFormsNext.WindowKit.Threading;

/// <summary>Identifies a dispatcher driven by a native event loop owned by the platform.</summary>
/// <remarks>
/// Application startup returns after showing its initial window. Framework exit retires its
/// lifetime subscriptions, but must never enter, cancel or terminate the native event loop.
/// Implementations still provide real queued work and timer delivery through IDispatcherImpl.
/// This capability is explicit: a missing or uninitialized dispatcher is not an external loop.
/// </remarks>
[PrivateApi]
public interface IExternallyOwnedDispatcherImpl : IDispatcherImpl
{
}
