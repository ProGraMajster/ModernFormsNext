using Android.App;
using Android.Content;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Owns the process-lifetime Android application context used by the backend.
/// </summary>
/// <remarks>
/// This object never retains an activity. Activity lifetime is tracked separately so rotation and
/// background transitions cannot leak a destroyed UI host.
/// </remarks>
public sealed class AndroidApplicationContext
{
    /// <summary>
    /// Creates a normalized application-context wrapper.
    /// </summary>
    /// <param name="context">Any context belonging to the host application.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the context cannot be resolved to an <see cref="Application"/> instance required
    /// for lifecycle callback registration.
    /// </exception>
    public AndroidApplicationContext(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Context = context.ApplicationContext ?? context;
        Application = Context as Application
            ?? throw new ArgumentException(
                "The supplied context does not expose an Android Application instance. " +
                "Pass the Application or an Activity from the host process.",
                nameof(context));
    }

    /// <summary>
    /// Gets the normalized process-wide Android context.
    /// </summary>
    public Context Context { get; }

    /// <summary>
    /// Gets the host application used to register lifecycle callbacks.
    /// </summary>
    public Application Application { get; }
}
