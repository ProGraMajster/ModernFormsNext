using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext;

public sealed partial class ResourceDictionary
{
    [ThreadStatic]
    private static int factoryDepth;

    /// <summary>Evaluates a factory once and stores its result as an ordinary local resource.</summary>
    /// <param name="key">The non-null resource key to add or replace.</param>
    /// <param name="factory">Trusted application code producing a value, including null.</param>
    /// <returns>The value stored by this registration.</returns>
    /// <remarks>
    /// <para>
    /// Creation is eager and synchronous on the UI dispatcher thread. There is one invocation per
    /// explicit call, no lazy execution, automatic retry, dependency tracking or retained delegate.
    /// The result stays cached until the entry is replaced, removed or cleared. Failure before
    /// publication leaves the old entry intact. A callback that mutates this dictionary prevents
    /// the outer registration from overwriting its newer state. Observer exceptions after publication
    /// follow the ordinary indexer contract: the new value has already been stored.
    /// </para>
    /// <para>
    /// The application owns all produced objects, including disposable results and results of a
    /// rejected reentrant registration. The dictionary never disposes resources. Create/replace live
    /// resources on their owning UI thread. Factories are code-only and are never serialized or run
    /// by Designer metadata/preview. Nested factory calls are limited to 32 across dictionaries.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// resources.SetFactory("Card.Background", static () => new SolidColorBrush(Color.CornflowerBlue));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">The key or factory is null.</exception>
    /// <exception cref="InvalidOperationException">Called outside the UI thread, nested too deeply,
    /// or the dictionary changed during creation.</exception>
    public object? SetFactory(object key, Func<object?> factory)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        IPlatformDispatcher? dispatcher = PlatformServiceRegistry.GetService<IPlatformDispatcher>();
        if (!(dispatcher?.CheckAccess() ?? Dispatcher.UIThread.CheckAccess()))
            throw new InvalidOperationException("Resource factories must run on the owning UI dispatcher thread.");
        if (factoryDepth >= 32)
            throw new InvalidOperationException("Resource factory nesting cannot exceed 32 calls.");

        long version;
        lock (syncRoot)
            version = mutationVersion;

        object? value;
        factoryDepth++;
        try
        {
            value = factory();
        }
        finally
        {
            factoryDepth--;
        }
        SetValue(key, value, version);
        return value;
    }
}
