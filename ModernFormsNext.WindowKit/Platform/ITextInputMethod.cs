using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Optional native text-service feature offered by a window or embedded surface backend.</summary>
/// <remarks>
/// Call on the owning UI thread. Clients are borrowed; never dispose their controls/documents.
/// Setting null retires the native connection. A new native connection captures the offered
/// instance, preventing queued callbacks from being redirected to a later focus owner.
/// </remarks>
public interface ITextInputMethod
{
    /// <summary>Attaches one borrowed, revocable host-coordinate client or clears native ownership.</summary>
    /// <param name="client">The current host session, or null to relinquish text ownership.</param>
    void SetClient(ITextInputClient? client);

    /// <summary>Requests native software keyboard visibility where the backend supports it.</summary>
    /// <param name="visible">True to show; false to hide.</param>
    /// <returns>Whether this backend accepted the request; actual visibility remains OS policy.</returns>
    bool SetKeyboardVisible(bool visible);
}
