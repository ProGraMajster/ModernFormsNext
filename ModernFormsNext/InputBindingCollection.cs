using System.Collections.ObjectModel;

namespace ModernFormsNext;

/// <summary>Owns the ordered input bindings for one control, window or application scope.</summary>
/// <remarks>
/// The first added matching available command wins. Unavailable bindings allow later registrations
/// and outer scopes to match. Duplicate gestures are allowed, but a binding object has one owner.
/// Read and mutate on the owning UI thread. Clearing/removing releases binding ownership, without
/// disposing commands or parameters. Scope disposal clears registrations and diagnostics observers.
/// Bindings are runtime-only and are not serialized by the Designer.
/// </remarks>
public sealed class InputBindingCollection : Collection<InputBinding>
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private object? scope;
    private bool disposed;

    internal InputBindingCollection(object? scope) => this.scope = scope;

    /// <summary>Occurs synchronously for invalid/duplicate registrations and matching command outcomes.</summary>
    /// <remarks>Optional; no diagnostic event args are allocated without a subscriber. Observers must not mutate input state.</remarks>
    public event EventHandler<InputBindingDiagnosticEventArgs>? Diagnostic;

    internal int Version { get; private set; }
    internal Control? ControlScope => scope as Control;
    internal bool IsActive => !disposed && scope switch
    {
        Control control => !control.IsDisposed && control.Enabled && control.Visible,
        WindowBase window => !window.InputBindingsClosed,
        _ => !Application.IsExiting
    };

    internal void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(disposed || scope is Control { IsDisposed: true }, this);
        if (Environment.CurrentManagedThreadId != threadId)
            throw new InvalidOperationException("Input binding collections must be accessed on their owning UI thread.");
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, InputBinding item)
    {
        VerifyItem(item);
        base.InsertItem(index, item);
        item.Owner = this;
        BindingChanged(item);
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, InputBinding item)
    {
        VerifyAccess();
        if (ReferenceEquals(this[index], item)) return;
        VerifyItem(item);
        this[index].Owner = null;
        base.SetItem(index, item);
        item.Owner = this;
        BindingChanged(item);
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        VerifyAccess();
        this[index].Owner = null;
        base.RemoveItem(index);
        Version++;
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        VerifyAccess();
        ClearCore();
    }

    private void VerifyItem(InputBinding item)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(item);
        item.VerifyAccess();
        if (item.Owner is not null)
            throw new ArgumentException("An input binding can belong to only one collection and cannot be added twice.", nameof(item));
    }

    internal void BindingChanged(InputBinding binding)
    {
        Version++;
        if (binding.Command is null || binding.Gesture.Key == Keys.None)
            Report(InputBindingDiagnosticKind.InvalidBinding, binding);
        else if (Diagnostic is not null)
            for (int i = 0; i < Count; i++)
                if (!ReferenceEquals(this[i], binding) && this[i].Gesture == binding.Gesture)
                {
                    Report(InputBindingDiagnosticKind.DuplicateGesture, binding);
                    break;
                }
    }

    internal void Report(InputBindingDiagnosticKind kind, InputBinding binding)
    {
        var handler = Diagnostic;
        if (handler is not null)
            handler(this, new InputBindingDiagnosticEventArgs(kind, binding));
    }

    private void ClearCore()
    {
        foreach (var binding in Items) binding.Owner = null;
        base.ClearItems();
        Version++;
    }

    // Disposal may also originate from Control's finalizer. Releasing managed references here
    // does not call application code, dispose commands or require dispatcher work.
    internal void Release()
    {
        if (disposed) return;
        disposed = true;
        ClearCore();
        Diagnostic = null;
        scope = null;
    }
}
