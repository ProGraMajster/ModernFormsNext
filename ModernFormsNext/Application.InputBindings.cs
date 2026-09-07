namespace ModernFormsNext;

public static partial class Application
{
    private static InputBindingCollection? inputBindings;

    /// <summary>Gets runtime keyboard bindings shared by all windows and standalone control surfaces.</summary>
    /// <remarks>
    /// Initialize, access and mutate on the application UI thread. This is the final fallback after
    /// focused-control, ancestor and window bindings. Only input delivered to a framework window or
    /// surface is observed; these are not system-wide hotkeys. Existing application shutdown paths
    /// release registrations and diagnostic handlers. Explicitly remove registrations that capture
    /// shorter-lived objects before shutdown. No command or parameter object is disposed.
    /// </remarks>
    /// <example><code>
    /// Application.InputBindings.Add(new KeyBinding(refresh, new KeyGesture(Keys.F5)));
    /// </code></example>
    public static InputBindingCollection InputBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(is_exiting, typeof(Application));
            var bindings = inputBindings ??= new InputBindingCollection(null);
            bindings.VerifyAccess();
            return bindings;
        }
    }

    internal static bool IsExiting => is_exiting;
    internal static InputBindingCollection? InputBindingsInternal => inputBindings;

    internal static void ReleaseInputBindings()
    {
        inputBindings?.Release();
        inputBindings = null;
    }
}
