namespace ModernFormsNext.WindowKit.Backend.Android.Lifecycle;

/// <summary>
/// Describes the Android host state observed by the WindowKit activity tracker.
/// </summary>
public enum AndroidApplicationLifecycleState
{
    /// <summary>No activity lifecycle callback has been observed.</summary>
    Unknown,
    /// <summary>An activity exists but has not yet entered the foreground.</summary>
    Created,
    /// <summary>An activity is resumed and can present UI.</summary>
    Foreground,
    /// <summary>The known activity is paused or stopped and must not present permission UI.</summary>
    Background,
    /// <summary>The last known activity was destroyed and no replacement is active.</summary>
    NoActivity
}
