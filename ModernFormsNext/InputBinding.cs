using System.Diagnostics;
using System.Windows.Input;

namespace ModernFormsNext;

/// <summary>Associates a concrete application command and parameter with one keyboard gesture.</summary>
/// <remarks>
/// This Phase 2 contract performs no command-target or handler routing. Create and mutate a binding
/// on its UI thread. One binding belongs to at most one collection; share the ICommand between
/// separate bindings instead. Removal releases ownership without disposing the command or parameter.
/// There are no CanExecuteChanged subscriptions: keyboard availability is evaluated on activation.
/// </remarks>
[DebuggerDisplay("{Gesture}")]
public abstract class InputBinding
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private ICommand? command;
    private object? parameter;
    private KeyGesture gesture;

    /// <summary>Initializes a binding to a concrete command and gesture.</summary>
    /// <param name="command">The command; null represents an inactive binding.</param>
    /// <param name="gesture">The gesture; its default value represents an inactive binding.</param>
    protected InputBinding(ICommand? command, KeyGesture gesture)
    {
        this.command = command;
        this.gesture = gesture;
    }

    /// <summary>Gets or sets the concrete command; null makes this binding inactive.</summary>
    /// <remarks>Set on the creating UI thread. Replacement affects the next evaluation; no command is disposed.</remarks>
    public ICommand? Command
    {
        get => command;
        set { VerifyAccess(); if (ReferenceEquals(command, value)) return; command = value; Changed(); }
    }

    /// <summary>Gets or sets the nullable parameter passed unchanged to CanExecute and Execute.</summary>
    /// <remarks>Set on the creating UI thread. Mutating a parameter object's contents requires no requery event for keyboard bindings.</remarks>
    public object? CommandParameter
    {
        get => parameter;
        set { VerifyAccess(); if (ReferenceEquals(parameter, value)) return; parameter = value; Changed(); }
    }

    /// <summary>Gets or sets the gesture. Its default value makes this binding inactive.</summary>
    /// <remarks>Set on the creating UI thread. Changes do not trigger layout or rendering.</remarks>
    public KeyGesture Gesture
    {
        get => gesture;
        set { VerifyAccess(); if (gesture == value) return; gesture = value; Changed(); }
    }

    internal InputBindingCollection? Owner { get; set; }
    internal int Version { get; private set; }

    internal void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Input bindings must be accessed on their creating UI thread.");
        Owner?.VerifyAccess();
    }

    private void Changed() { Version++; Owner?.BindingChanged(this); }
}
