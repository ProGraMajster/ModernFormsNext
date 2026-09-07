namespace ModernFormsNext;

/// <summary>Identifies a registration or execution observation from an input-binding collection.</summary>
public enum InputBindingDiagnosticKind
{
    /// <summary>A registration has no command or a default invalid gesture.</summary>
    InvalidBinding,
    /// <summary>Another registration in the same collection has the same gesture; insertion order decides precedence.</summary>
    DuplicateGesture,
    /// <summary>A matching command returned false from CanExecute; lookup may fall back.</summary>
    CommandUnavailable,
    /// <summary>A matching binding's command was invoked successfully.</summary>
    Executed
}

/// <summary>Describes an opt-in input-binding diagnostic without formatting parameter contents.</summary>
/// <remarks>Delivered synchronously on the UI thread. Observers must not mutate input state; their exceptions propagate.</remarks>
public sealed class InputBindingDiagnosticEventArgs : EventArgs
{
    internal InputBindingDiagnosticEventArgs(InputBindingDiagnosticKind kind, InputBinding binding)
    {
        Kind = kind;
        Binding = binding;
    }

    /// <summary>Gets the observation kind.</summary>
    public InputBindingDiagnosticKind Kind { get; }
    /// <summary>Gets the observed binding. This event does not transfer ownership.</summary>
    public InputBinding Binding { get; }
}
