using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;

namespace ModernFormsNext.Testing;

/// <summary>Represents one immutable, detached node in a captured ModernFormsNext control tree.</summary>
/// <remarks>
/// Geometry is read from live production controls at capture time. Later control mutations do not
/// change this snapshot. <see cref="DeviceBounds"/> is a deterministic edge-scaled projection for
/// diagnostics; it is not a rendered bitmap or platform pixel assertion.
/// </remarks>
public sealed class ControlTreeSnapshot
{
    private readonly ReadOnlyCollection<ControlTreeSnapshot> children;

    internal ControlTreeSnapshot(
        string path,
        string name,
        string typeName,
        Rectangle bounds,
        Rectangle clientRectangle,
        Rectangle displayRectangle,
        Rectangle deviceBounds,
        bool visible,
        bool enabled,
        IEnumerable<ControlTreeSnapshot> children)
    {
        Path = path;
        Name = name;
        TypeName = typeName;
        Bounds = bounds;
        ClientRectangle = clientRectangle;
        DisplayRectangle = displayRectangle;
        DeviceBounds = deviceBounds;
        Visible = visible;
        Enabled = enabled;
        this.children = Array.AsReadOnly(children.ToArray());
    }

    /// <summary>Gets the stable name/index path used by failure diagnostics.</summary>
    public string Path { get; }

    /// <summary>Gets the control or form name captured at snapshot time.</summary>
    public string Name { get; }

    /// <summary>Gets the short CLR type name captured at snapshot time.</summary>
    public string TypeName { get; }

    /// <summary>Gets bounds relative to the parent in logical pixels.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Gets the control client rectangle in its local logical coordinates.</summary>
    public Rectangle ClientRectangle { get; }

    /// <summary>Gets the layout display rectangle in local logical coordinates.</summary>
    public Rectangle DisplayRectangle { get; }

    /// <summary>Gets bounds projected to deterministic device pixels using the host render scale.</summary>
    public Rectangle DeviceBounds { get; }

    /// <summary>Gets whether the node was effectively visible when captured.</summary>
    public bool Visible { get; }

    /// <summary>Gets whether the node was effectively enabled when captured.</summary>
    public bool Enabled { get; }

    /// <summary>Gets detached child snapshots in framework collection order.</summary>
    public IReadOnlyList<ControlTreeSnapshot> Children => children;

    /// <summary>Returns a readable indented control-tree dump for assertions and failure logs.</summary>
    /// <returns>The complete tree dump.</returns>
    public string Dump()
    {
        var builder = new StringBuilder();
        AppendDump(builder, depth: 0);
        return builder.ToString().TrimEnd();
    }

    /// <inheritdoc/>
    public override string ToString() => Dump();

    internal string GetStabilitySignature()
    {
        var builder = new StringBuilder();
        AppendStabilitySignature(builder);
        return builder.ToString();
    }

    private void AppendDump(StringBuilder builder, int depth)
    {
        builder.Append(' ', depth * 2);
        builder.Append(TypeName);
        builder.Append(' ');
        builder.Append(string.IsNullOrWhiteSpace(Name) ? "<unnamed>" : Name);
        builder.Append(" [");
        builder.Append(Bounds.X);
        builder.Append(',');
        builder.Append(Bounds.Y);
        builder.Append(',');
        builder.Append(Bounds.Width);
        builder.Append(',');
        builder.Append(Bounds.Height);
        builder.AppendLine("]");

        foreach (ControlTreeSnapshot child in children)
            child.AppendDump(builder, depth + 1);
    }

    private void AppendStabilitySignature(StringBuilder builder)
    {
        builder.Append(Path).Append('|')
            .Append(Bounds).Append('|')
            .Append(ClientRectangle).Append('|')
            .Append(DisplayRectangle).Append('|')
            .Append(Visible).Append('|')
            .Append(Enabled).AppendLine();
        foreach (ControlTreeSnapshot child in children)
            child.AppendStabilitySignature(builder);
    }
}

internal static class ControlTreeSnapshotCapture
{
    internal static ControlTreeSnapshot Capture(Form form, double renderScale)
    {
        ArgumentNullException.ThrowIfNull(form);
        var logicalBounds = new Rectangle(Point.Empty, form.ClientSize);
        string name = string.IsNullOrWhiteSpace(form.Name) ? form.GetType().Name : form.Name;
        return new ControlTreeSnapshot(
            name,
            name,
            form.GetType().Name,
            logicalBounds,
            logicalBounds,
            logicalBounds,
            ToDeviceBounds(logicalBounds, renderScale),
            form.Visible,
            enabled: true,
            CaptureChildren(form.Controls, name, renderScale));
    }

    internal static ControlTreeSnapshot Capture(Control root, double renderScale)
    {
        ArgumentNullException.ThrowIfNull(root);
        string name = string.IsNullOrWhiteSpace(root.Name) ? root.GetType().Name : root.Name;
        return CaptureControl(root, name, name, renderScale, rootBounds: true);
    }

    private static IReadOnlyList<ControlTreeSnapshot> CaptureChildren(
        Control.ControlCollection controls,
        string parentPath,
        double renderScale)
    {
        var snapshots = new List<ControlTreeSnapshot>(controls.Count);
        for (var index = 0; index < controls.Count; index++)
        {
            Control control = controls[index];
            string fallback = $"{control.GetType().Name}[{index}]";
            string name = string.IsNullOrWhiteSpace(control.Name) ? fallback : control.Name;
            snapshots.Add(CaptureControl(control, $"{parentPath}.{name}", name, renderScale, rootBounds: false));
        }

        return snapshots;
    }

    private static ControlTreeSnapshot CaptureControl(
        Control control,
        string path,
        string name,
        double renderScale,
        bool rootBounds)
    {
        Rectangle bounds = rootBounds
            ? new Rectangle(Point.Empty, control.Size)
            : control.Bounds;
        return new ControlTreeSnapshot(
            path,
            name,
            control.GetType().Name,
            bounds,
            control.ClientRectangle,
            control.DisplayRectangle,
            ToDeviceBounds(bounds, renderScale),
            control.Visible,
            control.Enabled,
            CaptureChildren(control.Controls, path, renderScale));
    }

    private static Rectangle ToDeviceBounds(Rectangle logicalBounds, double renderScale)
    {
        var left = (int)Math.Round(logicalBounds.Left * renderScale);
        var top = (int)Math.Round(logicalBounds.Top * renderScale);
        var right = (int)Math.Round(logicalBounds.Right * renderScale);
        var bottom = (int)Math.Round(logicalBounds.Bottom * renderScale);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
