using System.ComponentModel;
using System.Drawing;

namespace ModernFormsNext;

/// <summary>Hosts one ordinary content control using the canonical child collection.</summary>
/// <remarks>
/// Content fills the logical padded area, respecting its margin and normal bounds constraints.
/// Replacing content detaches the previous control without disposing it; page disposal owns the
/// current child. Use a Panel or another ordinary container when the page needs multiple controls.
/// Dock/Anchor properties are retained; this single-content slot supplies the outer fill bounds.
/// </remarks>
/// <example><code>
/// var page = new ContentPage { Title = "Details", Padding = new Padding(12) };
/// page.Content = new Label { Text = "Document details" };
/// </code></example>
[DefaultProperty(nameof(Content))]
public class ContentPage : Page
{
    private long contentVersion;

    /// <summary>Creates an empty page without a native host or active lifecycle.</summary>
    public ContentPage() { }

    /// <summary>Gets or replaces the sole authored child, or null for an empty page.</summary>
    /// <remarks>
    /// UI thread only. The existing collection performs reparenting, resource/binding propagation,
    /// focus retirement and layout. Reentrant child changes take precedence over an older setter.
    /// The previous child is transferred to its caller; it is never implicitly disposed here.
    /// </remarks>
    [DefaultValue(null)]
    public Control? Content
    {
        get => Controls.Count == 0 ? null : Controls[0];
        set
        {
            VerifyPageAccess();
            long version = ++contentVersion;
            var previous = Content;
            if (ReferenceEquals(previous, value)) return;
            if (value is { IsDisposed: true } || value?.Disposing == true) throw new ObjectDisposedException(nameof(value));
            for (Control? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, value)) throw new ArgumentException("Content cannot contain its own page.", nameof(value));
            var incomingParent = value?.Parent;
            long? incomingVersion = value?.AccessibilityTreeVersion;
            if (previous is not null)
            {
                Controls.Remove(previous);
                // OnControlRemoved commits one stamp before its application callbacks.
                if (IsRetiringPage || contentVersion != version + 1) return;
                version = contentVersion;
            }
            if (!IsRetiringPage && contentVersion == version && value is not null &&
                ReferenceEquals(value.Parent, incomingParent) && value.AccessibilityTreeVersion == incomingVersion) Controls.Add(value);
        }
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(400, 300);
    /// <inheritdoc/>
    protected override ControlCollection CreateControlsInstance() => new ContentCollection(this);
    /// <inheritdoc/>
    protected override void OnControlAdded(EventArgs<Control> e) { contentVersion++; base.OnControlAdded(e); }
    /// <inheritdoc/>
    protected override void OnControlRemoved(EventArgs<Control> e) { contentVersion++; base.OnControlRemoved(e); }
    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Content is { IsDisposed: false } child)
        {
            var area = LogicalPaddedClientRectangle;
            var margin = child.Margin;
            child.Bounds = new(area.X + margin.Left, area.Y + margin.Top,
                Math.Max(0, area.Width - margin.Horizontal), Math.Max(0, area.Height - margin.Vertical));
        }
    }

    private sealed class ContentCollection(ContentPage owner) : ControlCollection(owner)
    {
        public override void Insert(int index, Control item)
        {
            ArgumentNullException.ThrowIfNull(item);
            owner.VerifyPageAccess();
            if (Count != 0 && !Contains(item)) throw new InvalidOperationException("ContentPage accepts one content control. Use Content to replace it.");
            // Removing from a foreign parent can populate this slot reentrantly. Perform that
            // canonical removal before the final slot check, preserving the newer assignment.
            for (Control? ancestor = owner; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, item)) throw new ArgumentException("Content cannot contain its own page.", nameof(item));
            if (item.Parent is { } previous && !ReferenceEquals(previous, owner))
            {
                previous.Controls.Remove(item);
                if (item.Parent is not null || owner.IsRetiringPage || Count != 0) return;
            }
            base.Insert(index, item);
        }
    }
}
