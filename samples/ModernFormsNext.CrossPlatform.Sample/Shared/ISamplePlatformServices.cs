using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Supplies platform facts and operations to the shared sample application.
/// </summary>
/// <remarks>
/// Implementations live under <c>Platforms</c>. Keeping the contract here makes the visible
/// control tree and its behavior identical on Windows and Android.
/// </remarks>
public interface ISamplePlatformServices
{
    /// <summary>Gets the user-facing platform name.</summary>
    string PlatformName { get; }

    /// <summary>Gets the current operating-system description.</summary>
    string OperatingSystem { get; }

    /// <summary>Gets the active WindowKit backend description.</summary>
    string BackendName { get; }

    /// <summary>Gets the current native host lifecycle description.</summary>
    string HostState { get; }

    /// <summary>Gets the platform UI dispatcher.</summary>
    IPlatformDispatcher Dispatcher { get; }

    /// <summary>Gets a value indicating whether the sample permission action is available.</summary>
    bool SupportsPermissionAction { get; }

    /// <summary>Checks or requests the optional camera permission used by the sample.</summary>
    /// <returns>The resulting platform-neutral permission status.</returns>
    Task<PlatformPermissionStatus> RequestSamplePermissionAsync();
}
