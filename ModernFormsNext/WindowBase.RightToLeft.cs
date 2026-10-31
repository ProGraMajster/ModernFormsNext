using System.ComponentModel;

namespace ModernFormsNext;

public abstract partial class WindowBase
{
    private static readonly object s_rightToLeftChangedEvent = new();

    /// <summary>Gets or sets the layout reading direction of the window's existing control root.</summary>
    /// <remarks>
    /// The local default Inherit resolves to No at a window root. Flow/Table descendants inherit
    /// this setting unless they explicitly opt out. The same control adapter owns layout, focus,
    /// hit testing and accessibility; native window chrome is not mirrored. Change on the owning
    /// UI thread. This setting does not select a culture or enable a separate bidi text engine.
    /// </remarks>
    /// <example><code>
    /// using var form = new Form { RightToLeft = RightToLeft.Yes };
    /// form.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill });
    /// </code></example>
    /// <exception cref="InvalidEnumArgumentException">The selection is not No, Yes or Inherit.</exception>
    [AmbientValue(RightToLeft.Inherit)]
    [Localizable(true)]
    public RightToLeft RightToLeft { get => adapter.RightToLeft; set => adapter.RightToLeft = value; }

    /// <summary>Occurs after the existing root's effective layout reading direction changes.</summary>
    /// <remarks>Raised synchronously with this window as sender, before descendant notifications complete.</remarks>
    public event EventHandler? RightToLeftChanged
    {
        add => Events.AddHandler(s_rightToLeftChangedEvent, value);
        remove => Events.RemoveHandler(s_rightToLeftChangedEvent, value);
    }

    /// <summary>Raises <see cref="RightToLeftChanged"/> for the existing control root.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Called on the owning UI thread. Overrides must call base.</remarks>
    protected virtual void OnRightToLeftChanged(EventArgs e)
        => (Events[s_rightToLeftChangedEvent] as EventHandler)?.Invoke(this, e);

    private bool ShouldSerializeRightToLeft() => adapter.ShouldSerializeRightToLeft();
    private void ResetRightToLeft() => RightToLeft = RightToLeft.Inherit;
}
