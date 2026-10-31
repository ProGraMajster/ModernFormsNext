using System;
using System.Drawing;
using ModernFormsNext.Accessibility;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>Finite shared RTL geometry, semantic and focus fixture for real platform hosts.</summary>
/// <remarks>Uses production layout/scroll/focus services. UI thread only; no culture or bidi-engine claim.</remarks>
internal sealed class RtlLayoutCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly FlowLayoutPanel flow;
    private readonly TableLayoutPanel table;
    private readonly FlowLayoutPanel mixed;
    private readonly FlowLayoutPanel overflow;
    private readonly Control leadingCell;
    private readonly Control trailingCell;
    private readonly Control span;
    private readonly Label status;
    private int assertions;

    internal Panel Root { get; }
    internal Button First { get; }
    internal Button Second { get; }
    internal int PaintCount => ((RtlRootPanel)Root).PaintCount;
    internal Rectangle FlowPixelBounds => new(flow.PointToScreen(Point.Empty), flow.LogicalToDeviceUnits(flow.Size));

    internal RtlLayoutCertification(Control.ControlCollection owner, Action<string> output)
    {
        this.owner = owner;
        this.output = output;
        Root = owner.Add(new RtlRootPanel { Dock = DockStyle.Fill, Name = "RtlLayout", RightToLeft = RightToLeft.Yes });
        flow = Root.Controls.Add(new FlowLayoutPanel {
            Bounds = new(12, 20, 330, 80), Padding = new Padding(13, 7, 7, 11), Name = "RtlFlow"
        });
        First = flow.Controls.Add(new Button {
            Size = new(105, 40), Text = "مرحبا", AccessibleName = "مرحبا", TabIndex = 0,
            Margin = new Padding(3, 5, 9, 7), BackColor = SKColors.CornflowerBlue
        });
        Second = flow.Controls.Add(new Button {
            Size = new(105, 40), Text = "שלום", AccessibleName = "שלום", TabIndex = 1, BackColor = SKColors.LightSalmon
        });
        table = Root.Controls.Add(new TableLayoutPanel {
            Bounds = new(12, 118, 330, 100), ColumnCount = 3, RowCount = 2, Padding = new Padding(5, 3, 11, 7)
        });
        foreach (float width in new[] { 33f, 33f, 34f }) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        leadingCell = new Label { Size = new(82, 30), Text = "עמודה 0", Margin = new Padding(3, 5, 9, 7) };
        trailingCell = new Label { Size = new(82, 30), Text = "عمود 2" };
        span = new Panel { Dock = DockStyle.Fill, BackColor = SKColors.LightSeaGreen };
        table.Controls.Add(leadingCell, 0, 0);
        table.Controls.Add(trailingCell, 2, 0);
        table.Controls.Add(span, 0, 1);
        table.SetColumnSpan(span, 2);
        mixed = new FlowLayoutPanel { Size = new(82, 32), RightToLeft = RightToLeft.No, Margin = Padding.Empty };
        mixed.Controls.Add(new Panel { Size = new(24, 20), BackColor = SKColors.CornflowerBlue, Margin = new Padding(1) });
        mixed.Controls.Add(new Panel { Size = new(24, 20), BackColor = SKColors.LightSalmon, Margin = new Padding(1) });
        table.Controls.Add(mixed, 2, 1);
        overflow = Root.Controls.Add(new FlowLayoutPanel {
            Bounds = new(12, 238, 330, 76), AutoScroll = true, WrapContents = false,
            Padding = new Padding(13, 7, 7, 11), Name = "RtlOverflow"
        });
        for (int i = 0; i < 8; i++) overflow.Controls.Add(new Button { Size = new(70, 35), Text = i.ToString(), Margin = new Padding(3) });
        status = Root.Controls.Add(new Label { Bounds = new(12, 334, 330, 68), Multiline = true });
        Root.BringToFront();
    }

    internal void Exercise()
    {
        Check(Root.FindForm() is not null && Root.Created && First.Created && table.Created, "attached-production-tree");
        PrepareDirection(true);
        Check(First.RightToLeft == RightToLeft.Yes && table.RightToLeft == RightToLeft.Yes, "inherited-direction");
        Check(First.Left > Second.Left && leadingCell.Left > trailingCell.Left, "visual-order");
        Check(ReferenceEquals(table.GetControlFromPosition(0, 0), leadingCell)
            && ReferenceEquals(table.GetControlFromPosition(1, 1), span), "logical-cell-and-span-identity");
        Check(mixed.RightToLeft == RightToLeft.No && mixed.Controls[0].Left < mixed.Controls[1].Left, "explicit-mixed-boundary");
        Check(ReferenceEquals(flow.GetNextControl(First), Second)
            && ReferenceEquals(flow.GetNextControl(Second, false), First), "authored-forward-reverse-traversal");
        Check(First.AccessibilityObject.Name == "مرحبا" && Second.AccessibilityObject.Name == "שלום", "unicode-semantic-names");
        var peer = First.AccessibilityObject;
        long identity = peer.RuntimeId;
        Rectangle bounds = peer.Bounds;
        Check(ReferenceEquals(flow.AccessibilityObject.HitTest(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2), peer),
            "semantic-hit-follows-rtl-bounds");
        foreach (Control child in overflow.Controls)
        {
            Check(child.AccessibilityObject.PerformAction(AccessibleActions.ScrollIntoView), "canonical-reveal");
            Check(overflow.AccessibilityObject.ScrollInfo!.Value.ViewportBounds.Contains(child.AccessibilityObject.Bounds), "revealed-child-visible");
            var offset = new Point(overflow.HorizontalScrollProperties.Value, overflow.VerticalScrollProperties.Value);
            overflow.PerformLayout();
            Check(new Point(overflow.HorizontalScrollProperties.Value, overflow.VerticalScrollProperties.Value) == offset,
                "relayout-preserves-offset");
        }
        overflow.Controls[0].AccessibilityObject.PerformAction(AccessibleActions.ScrollIntoView);
        PrepareDirection(false);
        Check(First.Left < Second.Left && leadingCell.Left < trailingCell.Left, "live-ltr-replacement");
        PrepareDirection(true);
        Check(peer.RuntimeId == identity && First.Left > Second.Left, "stable-semantic-identity");
        First.Select();
        Check(First.Selected && !Second.Selected, "canonical-keyboard-owner");
        output($"RTL_LAYOUT:CONTEXT scale={Root.Scaling} flow={flow.Bounds} table={table.Bounds}");
        output($"RTL_LAYOUT:PASS:{assertions}");
    }

    internal void PrepareDirection(bool rtl)
    {
        Root.RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        flow.PerformLayout(); table.PerformLayout(); mixed.PerformLayout(); overflow.PerformLayout();
        status.Text = rtl ? "RTL · مرحبا · שלום\nLogical indexes and Tab order are preserved."
            : "LTR override\nThe same controls and semantic identities remain.";
        Root.Invalidate();
    }

    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("RTL layout: " + category);
        assertions++;
        output("RTL_LAYOUT:ASSERT:" + category);
    }

    public void Dispose() { owner.Remove(Root); Root.Dispose(); }

    private sealed class RtlRootPanel : Panel
    {
        internal int PaintCount { get; private set; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); PaintCount++; }
    }
}
