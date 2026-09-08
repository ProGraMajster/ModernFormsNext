using System.Collections.ObjectModel;

namespace ModernFormsNext;

/// <summary>Owns ordered runtime command handlers for one control, window or application.</summary>
/// <remarks>
/// Access and mutate on the creating UI thread. Duplicate commands are visited in insertion order
/// until handled. Unlike InputBindings, these registrations map a command to route handlers, not
/// a gesture to a command. An invocation snapshots registrations; edits affect the next invocation.
/// Edits requery affected commands and can update source enabled/rendering/accessibility state.
/// Removing a binding releases its ownership without disposing its command or delegate captures.
/// </remarks>
public sealed class CommandBindingCollection : Collection<CommandBinding>
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private object? scope;
    internal bool IsReleased { get; private set; }

    internal CommandBindingCollection(object? scope) => this.scope = scope;

    internal void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsReleased || scope is Control { IsDisposed: true } ||
            scope is WindowBase { InputBindingsClosed: true }, this);
        if (threadId != Environment.CurrentManagedThreadId)
            throw new InvalidOperationException("Command binding collections must be accessed on their owning UI thread.");
    }

    private void VerifyItem(CommandBinding item)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(item);
        item.Command.VerifyAccess();
        if (item.Owner is not null)
            throw new ArgumentException("A command binding can belong to only one collection and cannot be added twice.", nameof(item));
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, CommandBinding item)
    {
        VerifyItem(item);
        base.InsertItem(index, item);
        item.Owner = this;
        item.Command.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, CommandBinding item)
    {
        VerifyAccess();
        if (ReferenceEquals(this[index], item)) return;
        VerifyItem(item);
        var previous = this[index];
        base.SetItem(index, item);
        previous.Owner = null;
        item.Owner = this;
        previous.Command.RaiseCanExecuteChanged();
        if (!ReferenceEquals(previous.Command, item.Command)) item.Command.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        VerifyAccess();
        var previous = this[index];
        base.RemoveItem(index);
        previous.Owner = null;
        previous.Command.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        VerifyAccess();
        var commands = new HashSet<RoutedCommand>();
        foreach (var binding in Items) commands.Add(binding.Command);
        ClearCore();
        foreach (var command in commands) command.RaiseCanExecuteChanged();
    }

    internal CommandBinding[] Snapshot(RoutedCommand command)
    {
        VerifyAccess();
        List<CommandBinding>? matches = null;
        foreach (var binding in Items)
            if (ReferenceEquals(binding.Command, command)) (matches ??= []).Add(binding);
        return matches?.ToArray() ?? [];
    }

    private void ClearCore()
    {
        foreach (var binding in Items) binding.Owner = null;
        base.ClearItems();
    }

    // Control finalization can also release collections. Never invoke application delegates or
    // dispatcher work here. Existing weak source subscriptions do not retain disposed controls.
    internal void Release()
    {
        if (IsReleased) return;
        IsReleased = true;
        ClearCore();
        scope = null;
    }
}
