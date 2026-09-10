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

    internal static IWindowImpl? TryCreateWindow() => Current.Value?.CreateWindow();

    internal static bool HasActiveFactory
    {
        get
        {
            if (Current.Value is not { } registration) return false;
            ObjectDisposedException.ThrowIf(!registration.IsActive, "ModernFormsTestHost");
            return true;
        }
    }

    internal static IDisposable Push(Func<IWindowImpl> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (Current.Value?.IsActive == true)
            throw new InvalidOperationException("A ModernFormsNext test window factory is already active in this execution context.");

        var registration = new FactoryRegistration(factory);
        Current.Value = registration;
        return registration;
    }

    private sealed class FactoryRegistration(Func<IWindowImpl> factory) : IDisposable
    {
        private Func<IWindowImpl>? createWindow = factory;
        private readonly int ownerThreadId = Environment.CurrentManagedThreadId;

        internal bool IsActive => Volatile.Read(ref createWindow) is not null;

        internal IWindowImpl CreateWindow()
        {
            // A captured async context can outlive the host that supplied its factory. It must
            // neither resurrect that host nor silently fall through to native window creation.
            var activeFactory = Volatile.Read(ref createWindow)
                ?? throw new ObjectDisposedException("ModernFormsTestHost", "The captured headless window factory has expired.");
            if (Environment.CurrentManagedThreadId != ownerThreadId)
                throw new InvalidOperationException("Headless windows must be constructed on the test host UI thread.");
            return activeFactory();
        }

        public void Dispose()
        {
            // Revocation clears the bound host delegate in every captured ExecutionContext.
            // Clearing only AsyncLocal.Value would leave those contexts retaining the whole host.
            if (Interlocked.Exchange(ref createWindow, null) is null)
                return;
            if (!ReferenceEquals(Current.Value, this))
                throw new InvalidOperationException("The ModernFormsNext test window factory scopes were disposed out of order.");

            Current.Value = null;
        }
    }
}
