using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

/// <summary>Contains detached, content-free service capability and eligibility facts.</summary>
/// <param name="Service">A stable service category.</param>
/// <param name="Supported">Whether this backend implements the category.</param>
/// <param name="RequiresActivity">Whether starting UI requires a resumed Activity.</param>
/// <param name="RequiresPermission">Whether explicit permission or a URI grant is required.</param>
/// <param name="Status">Current eligibility without starting UI.</param>
/// <param name="Note">A fixed policy explanation, never user content.</param>
public sealed record AndroidServiceCapability(string Service, bool Supported, bool RequiresActivity,
    bool RequiresPermission, PlatformServiceStatus Status, string Note);

/// <summary>A detached service snapshot with no native objects, URI values or payloads.</summary>
/// <param name="NativeUiPending">Whether a native UI slot is occupied, including after managed cancellation.</param>
/// <param name="Shutdown">Whether services have been shut down.</param>
/// <param name="Services">Immutable capability rows.</param>
public sealed record AndroidServiceDiagnostics(bool NativeUiPending, bool Shutdown,
    IReadOnlyList<AndroidServiceCapability> Services);
