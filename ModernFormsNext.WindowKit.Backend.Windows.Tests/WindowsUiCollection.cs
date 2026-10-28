using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

/// <summary>Serializes tests that borrow framework UI services in this process.</summary>
/// <remarks>
/// Controls and native windows share the backend's cursor cache, theme state and service registry.
/// These services do not support concurrent UI roots on separate xUnit worker threads. Keep
/// process-only asynchronous drivers in <see cref="WindowsNativeProcessCollection"/>: awaiting
/// a child process can retire the worker that owns this process's native message window.
/// Both collections disable parallelization, so they also exclude each other's desktop work.
/// </remarks>
[CollectionDefinition(WindowsUiCollection.Name, DisableParallelization = true)]
public sealed class WindowsUiCollection
{
    /// <summary>Identifies tests that share Windows framework UI services.</summary>
    public const string Name = "Windows UI services";
}

/// <summary>Serializes asynchronous drivers whose native UI services belong to child processes.</summary>
/// <remarks>
/// Separate processes isolate framework services but share desktop focus, dialogs and visible
/// pixels. Disabling parallelization excludes all other collections while these hosts run,
/// including the in-process UI collection; it does not weaken native assertions or set test order.
/// </remarks>
[CollectionDefinition(WindowsNativeProcessCollection.Name, DisableParallelization = true)]
public sealed class WindowsNativeProcessCollection
{
    /// <summary>Identifies process-isolated native hosts that require exclusive desktop access.</summary>
    public const string Name = "Windows native processes";
}
