namespace ModernFormsNext;

public static partial class Application
{
    private static CommandBindingCollection? commandBindings;

    /// <summary>Gets terminal fallback handlers shared by application windows and control surfaces.</summary>
    /// <remarks>
    /// Access on the application UI thread. This terminal scope is visited after a valid target's
    /// control/window route; it does not make targetless calls available. Sender is typeof(Application).
    /// Shutdown releases registrations. Remove bindings capturing shorter-lived objects explicitly.
    /// No command is disposed and no global service/command registry is created.
    /// </remarks>
    public static CommandBindingCollection CommandBindings
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsExiting, typeof(Application));
            var bindings = commandBindings ??= new CommandBindingCollection(null);
            bindings.VerifyAccess();
            return bindings;
        }
    }

    internal static CommandBindingCollection? CommandBindingsInternal => commandBindings;

    internal static void ReleaseCommandBindings()
    {
        commandBindings?.Release();
        commandBindings = null;
    }
}
