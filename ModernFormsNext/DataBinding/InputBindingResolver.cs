using System.Windows.Input;

namespace ModernFormsNext.DataBinding;

// Shared behavior at the existing WindowBase and SkiaControlSurface key entry points.
// This looks up concrete bindings; it neither dispatches events nor routes command handlers.
internal sealed class InputBindingResolver
{
    private HashSet<Keys>? consumedKeys;

    internal bool ProcessKeyDown(KeyEventArgs e, Control? focused, Control root, WindowBase? window)
    {
        if (e.Handled) return true;
        if (!IsContextActive(root, window)) { Reset(); return false; }
        Keys key = e.KeyData & Keys.KeyCode;
        if (e.AltGraph)
        {
            // International text must never inherit suppression from a previous shortcut
            // if modifiers change while a physical key is held.
            consumedKeys?.Remove(key);
            return false;
        }

        List<Candidate>? candidates = null;
        // Existing focus bookkeeping can still point at a control detached/reparented by the
        // application. A window must never execute bindings from another control tree.
        if (focused is not null && !IsWithinRoot(focused, root)) focused = null;
        // Snapshot only matching registrations, before calling any application predicate. The
        // usual unmatched key does not allocate a candidate list or initialize empty scopes.
        for (Control? control = focused ?? root; control is not null; control = control.Parent)
        {
            Collect(control.InputBindingsInternal, e, ref candidates);
            if (ReferenceEquals(control, root)) break;
        }
        Collect(window?.InputBindingsInternal, e, ref candidates);
        Collect(Application.InputBindingsInternal, e, ref candidates);

        if (candidates is not null)
            foreach (var candidate in candidates)
            {
                if (!candidate.IsCurrent(root) || !IsContextActive(root, window)) break;
                var binding = candidate.Binding;
                ICommand? command = binding.Command;
                object? parameter = binding.CommandParameter;
                if (command is null)
                {
                    candidate.Collection.Report(InputBindingDiagnosticKind.InvalidBinding, binding);
                    if (!candidate.IsCurrent(root)) break;
                    continue;
                }

                // Gesture precedence ends here. Routed commands receive this input context, while
                // ordinary ICommand retains the Phase 1/2 direct call and evaluation count.
                CommandRouting.Invocation? invocation = null;
                bool available;
                if (command is RoutedCommand routed)
                {
                    var source = candidate.Collection.ControlScope;
                    // A disposed control can retain Parent and stale native focus bookkeeping.
                    // It is not an implicit routed target; explicit targets still fail closed.
                    var target = binding.CommandTarget ??
                        (focused is { IsDisposed: false, Disposing: false } ? focused : null) ?? source ?? root;
                    invocation = CommandRouting.Prepare(routed, parameter, target, source, root);
                    available = invocation is not null;
                }
                else
                    available = command.CanExecute(parameter);
                if (!candidate.IsCurrent(root) || !IsContextActive(root, window)) break;
                if (!available)
                {
                    candidate.Collection.Report(InputBindingDiagnosticKind.CommandUnavailable, binding);
                    if (!candidate.IsCurrent(root)) break;
                    continue;
                }

                // Consume before calling application code, including throwing commands. A focus
                // change or self-removal must not let KeyUp activate another button afterwards.
                (consumedKeys ??= []).Add(key);
                e.SuppressKeyPress = true;
                if (invocation is not null)
                    invocation.Execute();
                else if (command is DelegateCommand delegated)
                    delegated.ExecuteCore(parameter);
                else if (command is AsyncCommand asynchronous)
                    asynchronous.ExecuteCore(parameter);
                else
                    command.Execute(parameter);
                candidate.Collection.Report(InputBindingDiagnosticKind.Executed, binding);
                return true;
            }

        // Once a press was consumed, keep its remaining repeats/release out of normal control
        // input even if availability changes. Each repeat can still resolve an available fallback.
        if (consumedKeys?.Contains(key) == true)
        {
            e.SuppressKeyPress = true;
            return true;
        }
        return false;
    }

    internal bool ProcessKeyUp(KeyEventArgs e)
    {
        if (consumedKeys?.Remove(e.KeyData & Keys.KeyCode) != true) return false;
        e.SuppressKeyPress = true;
        return true;
    }

    internal void Reset() => consumedKeys?.Clear();

    private static bool IsContextActive(Control root, WindowBase? window)
        => !root.IsDisposed && root.Visible && root.Enabled && window?.InputBindingsClosed != true && !Application.IsExiting;

    private static bool IsWithinRoot(Control control, Control root)
    {
        for (Control? current = control; current is not null; current = current.Parent)
            if (ReferenceEquals(current, root)) return true;
        return false;
    }

    private static void Collect(InputBindingCollection? collection, KeyEventArgs e, ref List<Candidate>? candidates)
    {
        if (collection is null || collection.Count == 0 || !collection.IsActive) return;
        collection.VerifyAccess();
        int version = collection.Version;
        for (int i = 0; i < collection.Count; i++)
        {
            var binding = collection[i];
            if (binding.Gesture.Matches(e))
                (candidates ??= []).Add(new Candidate(binding, binding.Version, collection, version));
        }
    }

    private readonly record struct Candidate(InputBinding Binding, int BindingVersion, InputBindingCollection Collection, int CollectionVersion)
    {
        internal bool IsCurrent(Control root) => Collection.IsActive && Collection.Version == CollectionVersion &&
            Binding.Version == BindingVersion && ReferenceEquals(Binding.Owner, Collection) &&
            (Collection.ControlScope is not Control control || IsWithinRoot(control, root));
    }
}
