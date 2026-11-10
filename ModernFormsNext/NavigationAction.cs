using System.Windows.Input;

namespace ModernFormsNext;

/// <summary>Describes a runtime toolbar button for a page's navigation header.</summary>
/// <remarks>Commands and parameters are borrowed application objects. Designer documents do not instantiate them.</remarks>
public sealed class NavigationAction
{
    /// <summary>Creates an action that uses the framework's standard button command path.</summary>
    /// <param name="text">Visible and accessible action text.</param>
    /// <param name="command">The application command or scoped routed command.</param>
    /// <param name="parameter">An optional argument passed to the command.</param>
    public NavigationAction(string text, ICommand command, object? parameter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(command);
        Text = text;
        Command = command;
        Parameter = parameter;
    }

    /// <summary>Gets the button label and accessible name.</summary>
    public string Text { get; }
    /// <summary>Gets the borrowed command.</summary>
    public ICommand Command { get; }
    /// <summary>Gets the borrowed command parameter.</summary>
    public object? Parameter { get; }
}
