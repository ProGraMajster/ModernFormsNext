namespace ModernFormsNext.DataBinding;

// Handler routing is independent of input-binding lookup. Each invocation owns its snapshot,
// query cursor and arguments, including nested/recursive calls. There is no shared route state.
internal static class CommandRouting
{
    internal static Control? FindRoot(Control? control)
    {
        for (var current = control; current is not null; current = current.Parent)
            if (current.IsCommandRoutingRoot) return current;
        return null;
    }

    internal static bool IsWithin(Control control, Control boundary)
    {
        for (Control? current = control; current is not null; current = current.Parent)
            if (ReferenceEquals(current, boundary)) return true;
        return false;
    }

    internal static Invocation? Prepare(RoutedCommand command, object? parameter, Control? target,
        Control? source = null, Control? boundary = null)
    {
        command.VerifyAccess();
        var root = FindRoot(target);
        if (target is null || root is null || target.IsDisposed || target.Disposing || root.IsDisposed ||
            source?.IsDisposed == true || Application.IsExiting ||
            root is ControlAdapter { ParentForm.InputBindingsClosed: true } ||
            (boundary is not null && !IsWithin(target, boundary)))
        {
            command.Report(CommandRoutingDiagnosticKind.Failed, target, parameter,
                failureReason: target is null ? "No target context." : "Target is detached, disposed, closed or outside the source tree.");
            return null;
        }

        List<Node> nodes = [];
        // Represent the native adapter by its WindowBase. The standalone surface's borrowed root
        // is already in the chain; its infrastructure parent is not an application handler scope.
        for (var current = target; !ReferenceEquals(current, root); current = current.Parent!)
            nodes.Add(Capture(current, current.CommandBindingsInternal, command));
        WindowBase? window = (root as ControlAdapter)?.ParentForm;
        if (window is not null) nodes.Add(Capture(window, window.CommandBindingsInternal, command));
        nodes.Add(Capture(typeof(Application), Application.CommandBindingsInternal, command));
        var invocation = new Invocation(command, parameter, target, source, root, window, nodes.ToArray());
        return invocation.FindNextAvailable() ? invocation : null;
    }

    private static Node Capture(object owner, CommandBindingCollection? collection, RoutedCommand command)
        => new(owner, collection, collection?.Snapshot(command) ?? []);

    internal sealed class Invocation
    {
        private readonly RoutedCommand command;
        private readonly object? parameter;
        private readonly Control target;
        private readonly Control? source;
        private readonly Control root;
        private readonly WindowBase? window;
        private readonly Node[] nodes;
        private int nodeIndex, bindingIndex;
        private Node? selectedNode;
        private CommandBinding? selectedBinding;
        private bool executed;

        internal Invocation(RoutedCommand command, object? parameter, Control target, Control? source,
            Control root, WindowBase? window, Node[] nodes)
            => (this.command, this.parameter, this.target, this.source, this.root, this.window, this.nodes) =
                (command, parameter, target, source, root, window, nodes);

        private bool IsAlive(Node? node = null)
        {
            // Reparent/remove must not re-resolve Parent or focus. Lifetime is the exception:
            // captured owners that have been disposed must never receive callbacks.
            if (!target.IsDisposed && !target.Disposing && source?.IsDisposed != true && !root.IsDisposed &&
                window?.InputBindingsClosed != true && !Application.IsExiting &&
                node?.Collection?.IsReleased != true && node?.Owner is not Control { IsDisposed: true }) return true;
            Report(CommandRoutingDiagnosticKind.Failed, node, failureReason: "A captured target or owner was disposed or closed.");
            return false;
        }

        internal bool FindNextAvailable()
        {
            while (nodeIndex < nodes.Length)
            {
                Node node = nodes[nodeIndex];
                if (!IsAlive(node)) return false;
                if (bindingIndex == 0) Report(CommandRoutingDiagnosticKind.NodeVisited, node);
                while (bindingIndex < node.Bindings.Length)
                {
                    var binding = node.Bindings[bindingIndex++];
                    if (!IsAlive(node)) return false;
                    Report(CommandRoutingDiagnosticKind.BindingFound, node, binding);
                    if (!IsAlive(node)) return false;
                    var args = new CanExecuteCommandEventArgs(command, parameter, target, source);
                    try { binding.CanExecute?.Invoke(node.Owner, args); }
                    catch { ReportHandlerFailure(node, binding); throw; }
                    Report(CommandRoutingDiagnosticKind.CanExecuteEvaluated, node, binding, args.CanExecute);
                    if (args.Handled) Report(CommandRoutingDiagnosticKind.Handled, node, binding, args.CanExecute);
                    if (!IsAlive(node)) return false;
                    if (args.CanExecute)
                    {
                        selectedNode = node;
                        selectedBinding = binding;
                        return true;
                    }
                    if (args.Handled)
                    {
                        Report(CommandRoutingDiagnosticKind.Failed, node, binding, false, "CanExecute vetoed the remaining route.");
                        return false;
                    }
                }
                nodeIndex++;
                bindingIndex = 0;
            }
            if (!executed) Report(CommandRoutingDiagnosticKind.Failed, failureReason: "No available command binding.");
            return false;
        }

        internal void Execute()
        {
            command.VerifyAccess();
            do
            {
                if (selectedNode is not { } node || selectedBinding is not { } binding || !IsAlive(node)) return;
                var args = new ExecutedCommandEventArgs(command, parameter, target, source);
                try { binding.Executed(node.Owner, args); }
                catch { ReportHandlerFailure(node, binding); throw; }
                executed = true;
                Report(CommandRoutingDiagnosticKind.Executed, node, binding);
                if (args.Handled)
                {
                    Report(CommandRoutingDiagnosticKind.Handled, node, binding);
                    return;
                }
            } while (FindNextAvailable());
        }

        private void Report(CommandRoutingDiagnosticKind kind, Node? node = null, CommandBinding? binding = null,
            bool? available = null, string? failureReason = null)
            => command.Report(kind, target, parameter, node?.Owner, binding, available, failureReason);

        private void ReportHandlerFailure(Node node, CommandBinding binding)
        {
            // Never replace an application exception with a diagnostic observer exception.
            // Do not include exception messages, Data or parameter values in diagnostics.
            try { Report(CommandRoutingDiagnosticKind.Failed, node, binding, failureReason: "A command handler threw an exception."); }
            catch { }
        }
    }

    internal sealed record Node(object Owner, CommandBindingCollection? Collection, CommandBinding[] Bindings);
}
