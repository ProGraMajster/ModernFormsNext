using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

/// <summary>Serializes tests that borrow process-wide framework UI services in this test process.</summary>
/// <remarks>
/// Real controls and native windows share the backend's cursor cache, theme state and service
/// registry. Each test still creates and disposes its UI on its own thread; those services do
/// not support concurrent UI roots on separate xUnit worker threads. Classes that only exercise
/// independent providers or isolated child processes do not need this collection.
/// </remarks>
[CollectionDefinition(WindowsUiCollection.Name, DisableParallelization = true)]
public sealed class WindowsUiCollection
{
    /// <summary>Identifies tests that share the in-process Windows framework UI environment.</summary>
    public const string Name = "Windows UI services";
}
