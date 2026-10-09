namespace ModernFormsNext;

internal static class NativeViewOrdering
{
    // Only attached hosts are indexed, never controls or render frames. UI thread only.
    private static readonly List<NativeViewHost> hosts = [];
    private static readonly List<NativeViewHost> active = [];
    private static bool updating;
    internal static void Add(NativeViewHost host) { if (!hosts.Contains(host)) hosts.Add(host); }
    internal static void Remove(NativeViewHost host) => hosts.Remove(host);
    internal static void Update(WindowBase window)
    {
        if (updating || hosts.Count < 2) return;
        updating = true;
        try
        {
            active.Clear();
            foreach (var host in hosts)
                if (ReferenceEquals(host.NativeWindow, window) && host.Parent is not null) active.Add(host);
            active.Sort(Compare);
            WindowKit.Platform.INativeViewSession? previous = null;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].NativeSession is { } session)
                { session.PlaceAbove(previous); previous = session; }
        }
        finally { active.Clear(); updating = false; }
    }
    private static int Compare(NativeViewHost a, NativeViewHost b)
    {
        if (ReferenceEquals(a, b) || !ReferenceEquals(a.NativeWindow, b.NativeWindow)) return 0;
        // Compare the first differing siblings along existing root paths. No public ZIndex.
        Control first = a, second = b;
        int firstDepth = Depth(first), secondDepth = Depth(second);
        while (firstDepth > secondDepth) { first = first.Parent!; firstDepth--; }
        while (secondDepth > firstDepth) { second = second.Parent!; secondDepth--; }
        if (ReferenceEquals(first, second)) return Depth(a).CompareTo(Depth(b));
        while (!ReferenceEquals(first.Parent, second.Parent))
        { first = first.Parent!; second = second.Parent!; }
        return first.Parent is { } parent
            ? parent.Controls.GetChildIndex(first, false).CompareTo(parent.Controls.GetChildIndex(second, false)) : 0;
    }
    private static int Depth(Control control)
    { int depth = 0; for (; control.Parent is not null; control = control.Parent) depth++; return depth; }
}
