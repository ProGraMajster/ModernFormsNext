using System.Threading;
using ModernFormsNext.WindowKit.Platform;

namespace ModernFormsNext;

/// <summary>
/// Provides the assembly-internal window factory seam used by the supported Testing package.
/// </summary>
/// <remarks>
/// The scope is execution-context-local so normal application construction continues through
/// <see cref="WindowKit.Backend.FrameworkBootstrap"/>. The Testing package additionally serializes
/// active hosts because other application services remain process-wide.
/// </remarks>
internal static class TestWindowFactoryScope
{
    private static readonly AsyncLocal<FactoryRegistration?> Current = new();

    internal static IWindowImpl? TryCreateWindow() => Current.Value?.Factory();

    internal static IDisposable Push(Func<IWindowImpl> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (Current.Value is not null)
            throw new InvalidOperationException("A ModernFormsNext test window factory is already active in this execution context.");

        var registration = new FactoryRegistration(factory);
        Current.Value = registration;
        return new RestoreScope(registration);
    }

    private sealed record FactoryRegistration(Func<IWindowImpl> Factory);

    private sealed class RestoreScope(FactoryRegistration registration) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;
            if (!ReferenceEquals(Current.Value, registration))
                throw new InvalidOperationException("The ModernFormsNext test window factory scopes were disposed out of order.");

            Current.Value = null;
            disposed = true;
        }
    }
}
