using Android.OS;

namespace ModernFormsNext.WindowKit.Backend.Android;

/// <summary>
/// Exposes read-only Android runtime information without leaking it into shared WindowKit APIs.
/// </summary>
public sealed class AndroidPlatformInfo
{
    /// <summary>
    /// Gets the integer Android API level of the current device.
    /// </summary>
    public int SdkVersion => (int)Build.VERSION.SdkInt;

    /// <summary>
    /// Gets the Android release label reported by the device, when available.
    /// </summary>
    public string Release => Build.VERSION.Release ?? string.Empty;

    /// <summary>
    /// Determines whether the device is running at least the supplied Android API level.
    /// </summary>
    /// <param name="sdkVersion">The Android API level to compare.</param>
    /// <returns><see langword="true"/> when the device API level is equal or newer.</returns>
    public bool IsAtLeast(int sdkVersion) => SdkVersion >= sdkVersion;
}
