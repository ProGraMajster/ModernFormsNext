using Android.App;
using Android.Content.PM;
using Android.Content;
using ModernFormsNext.WindowKit.Backend.Android.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Provides the explicit Android host integration entry point for WindowKit.
/// </summary>
/// <remarks>
/// Call <see cref="Initialize"/> from the host application before using platform services. The
/// static facade retains the backend and application context for the process lifetime, while the
/// backend's lifecycle tracker keeps activities weakly referenced.
/// </remarks>
public static class AndroidWindowKit
{
    private static readonly object Sync = new();
    private static AndroidWindowKitBackend? current;

    /// <summary>
    /// Gets the stable Android logcat tag used by the backend and its Skia surface host.
    /// </summary>
    public const string LogTag = "ModernFormsNext";

    /// <summary>
    /// Gets a value indicating whether Android backend initialization completed.
    /// </summary>
    public static bool IsInitialized => current?.IsInitialized == true;

    /// <summary>
    /// Gets the initialized Android backend.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Initialize"/>.</exception>
    public static AndroidWindowKitBackend Current
        => current ?? throw new InvalidOperationException(
            "The Android WindowKit backend has not been initialized. " +
            "Call AndroidWindowKit.Initialize during Android application startup.");

    /// <summary>
    /// Initializes and registers the Android backend once for the current process.
    /// </summary>
    /// <param name="options">The application context and optional host integration settings.</param>
    /// <returns>The active Android backend.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when initialization is attempted with a different application context.
    /// </exception>
    public static AndroidWindowKitBackend Initialize(AndroidWindowKitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (Sync)
        {
            if (current is not null)
            {
                var requestedContext = options.ApplicationContext.ApplicationContext
                    ?? options.ApplicationContext;
                if (!ReferenceEquals(current.ApplicationContext.Context, requestedContext))
                {
                    throw new InvalidOperationException(
                        "The Android WindowKit backend is already initialized for a different " +
                        "Application Context.");
                }

                return current;
            }

            var backend = new AndroidWindowKitBackend(options);
            current = WindowKitBackendRegistry.Register(backend);
            return current;
        }
    }

    /// <summary>
    /// Supplies an already-created host activity to the weak lifecycle tracker.
    /// </summary>
    /// <param name="activity">The foreground host activity.</param>
    /// <remarks>
    /// This bridge is useful when initialization occurs inside the first activity's
    /// <c>OnCreate</c>. Subsequent transitions are tracked automatically by application lifecycle
    /// callbacks. The activity is not retained strongly.
    /// </remarks>
    public static void ObserveHostActivity(Activity activity)
        => Current.ActivityTracker.ObserveHostActivity(activity);

    /// <summary>Publishes a new Intent as normalized activation without recreating the application.</summary>
    /// <param name="activity">The activity receiving OnNewIntent.</param>
    /// <param name="intent">The newly delivered intent; null represents ordinary activation.</param>
    /// <remarks>
    /// Call on the Android main thread in OnNewIntent. Native intent objects are not retained by
    /// shared data; URI/content grants remain owned by Android. Initial activation and saved-state
    /// callbacks are observed automatically. Events from unknown or destroyed activities are ignored;
    /// a known paused activity can receive a new Intent. Application state restoration runs only
    /// for the first Activity created in a process, preserving newer live state during recreation.
    /// Invalid payloads and observer failures are reported
    /// through backend diagnostics without retaining payload contents or interrupting native callbacks.
    /// </remarks>
    public static void HandleNewIntent(Activity activity, Intent? intent)
        => Current.ActivityTracker.HandleNewIntent(activity, intent);

    /// <summary>Forwards SAF and chooser results from a custom Activity on the main thread.</summary>
    /// <param name="activity">The exact Activity receiving the callback.</param>
    /// <param name="requestCode">The reserved native request code.</param>
    /// <param name="resultCode">The Android result.</param>
    /// <param name="data">Transient returned data; native objects are not retained.</param>
    /// <returns>Whether the result belongs to the current service request.</returns>
    public static bool HandleActivityResult(Activity activity, int requestCode, Result resultCode, Intent? data)
        => Current.ServiceRequests.Handle(activity, requestCode, resultCode, data);

    /// <summary>Forwards a permission callback with explicit Activity identity.</summary>
    /// <param name="activity">The exact Activity receiving the callback.</param>
    /// <param name="requestCode">The native request code.</param>
    /// <param name="permissions">Native permission names.</param>
    /// <param name="grantResults">Returned grants.</param>
    /// <returns>Whether this callback completed the current request.</returns>
    public static bool HandleRequestPermissionsResult(Activity activity, int requestCode, string[] permissions, Permission[] grantResults)
        => Current.Permissions.HandleRequestPermissionsResult(activity, requestCode, permissions, grantResults);

    /// <summary>
    /// Forwards an activity permission callback to the central request coordinator.
    /// </summary>
    /// <param name="requestCode">The Android permission request code.</param>
    /// <param name="permissions">The Android permission names returned by the platform.</param>
    /// <param name="grantResults">The platform grant results.</param>
    /// <returns>
    /// <see langword="true"/> when the callback belonged to WindowKit; otherwise,
    /// <see langword="false"/> so the host can route it elsewhere.
    /// </returns>
    public static bool HandleRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
        => Current.Permissions.HandleRequestPermissionsResult(requestCode, permissions, grantResults);
}
