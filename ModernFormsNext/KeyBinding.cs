using System.Windows.Input;

namespace ModernFormsNext;

/// <summary>Invokes a concrete command when its keyboard gesture wins scoped input-binding lookup.</summary>
/// <remarks>
/// Uses KeyDown only, including backend-delivered repeats. Normal Button.Click is not synthesized.
/// Commands use their current parameter and a fresh CanExecute check. This type adds no routed commands.
/// </remarks>
/// <example><code>
/// form.InputBindings.Add(new KeyBinding(saveCommand, new KeyGesture(Keys.S, KeyModifiers.Control))
/// {
///     CommandParameter = document
/// });
/// </code></example>
public sealed class KeyBinding : InputBinding
{
    /// <summary>Creates a keyboard binding on the current UI thread.</summary>
    /// <param name="command">The concrete command, or null for an inactive binding.</param>
    /// <param name="gesture">The gesture, or its default value for an inactive binding.</param>
    public KeyBinding(ICommand? command, KeyGesture gesture) : base(command, gesture) { }
}
