using Android.App;
using Android.Content;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Configures Android WindowKit platform initialization.
/// </summary>
/// <param name="applicationContext">
/// An Android application or context from which the process-wide application context can be obtained.
/// </param>
/// <remarks>
/// The backend retains only the application context. If <see cref="ActivityProvider"/> is supplied,
/// the provider must not itself keep a destroyed activity alive. Initialize on the Android main
/// thread before accessing WindowKit dispatcher or permission services.
/// </remarks>
public sealed class AndroidWindowKitOptions(Context applicationContext)
{
    /// <summary>
    /// Gets the context supplied by the host. Initialization normalizes it to application scope.
    /// </summary>
    public Context ApplicationContext { get; } =
        applicationContext ?? throw new ArgumentNullException(nameof(applicationContext));

    /// <summary>
    /// Gets or sets an optional provider used when a host manages its activity separately.
    /// </summary>
    /// <remarks>
    /// Lifecycle callbacks remain the default and recommended source. The provider is evaluated on
    /// demand and should return <see langword="null"/> when its activity is finishing or destroyed.
    /// </remarks>
    public Func<Activity?>? ActivityProvider { get; set; }

    /// <summary>
    /// Gets or sets the maximum time a native permission dialog may remain unresolved.
    /// </summary>
    /// <remarks>
    /// The timeout protects queued requests from hanging indefinitely after unusual host lifecycle
    /// failures. The default is two minutes.
    /// </remarks>
    public TimeSpan PermissionRequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets an optional sink for actionable backend diagnostics.
    /// </summary>
    public Action<string>? DiagnosticSink { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether individual surface renders should be written to
    /// Android diagnostics.
    /// </summary>
    /// <remarks>
    /// Lifecycle, initialization, and failures are always reported. Per-frame messages are opt-in
    /// because they can be noisy while debugging pointer input or resizing.
    /// </remarks>
    public bool EnableDetailedDiagnostics { get; set; }
}
