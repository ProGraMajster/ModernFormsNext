using System.Drawing;
using System.Reflection;
using ModernFormsNext.Accessibility;
using SkiaSharp;

namespace ModernFormsNext.Validation;

/// <summary>Finite passive-tooltip fixture shared by deterministic and native certification hosts.</summary>
/// <remarks>UI thread only. Reflection reads the closed framework's owned popup for validation;
/// it never resolves project types, evaluates user data or adds a runtime service/public API.</remarks>
internal sealed class ToolTipCertification : IDisposable
{
    private readonly Control.ControlCollection owner;
    private readonly Action<string> output;
    private readonly Form form;
    private int assertions;
    internal Panel Root { get; }
    internal TextBox Editor { get; }
    internal ComboBox Choices { get; }
    internal ToolTip Tip { get; } = new()
    {
        BackColor = SKColors.LemonChiffon, ForeColor = SKColors.Black,
        BorderWidth = 0, BorderRadius = 0, ToolTipTitle = "Passive help"
    };
    internal PopupWindow Popup => (PopupWindow)(typeof(ToolTip).GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(Tip) ?? throw new InvalidOperationException("The certification popup was not created."));
    internal Control Content => Popup.Controls.Single();
    internal bool Settled => Popup.Visible && !Content.AccessibilityObject.State.HasFlag(AccessibleStates.Invisible);
    internal Rectangle PixelBounds => new(Popup.Location, Content.LogicalToDeviceUnits(Popup.Size));

    internal ToolTipCertification(Form form, Action<string> output)
    {
        this.form = form;
        owner = form.Controls;
        this.output = output;
        Root = owner.Add(new Panel { Dock = DockStyle.Fill, BackColor = SKColors.RoyalBlue, Name = "ToolTipCertification" });
        Editor = Root.Controls.Add(new TextBox { Bounds = new Rectangle(12, 16, 250, 36), Text = "Focus stays here" });
        Choices = Root.Controls.Add(new ComboBox { Bounds = new Rectangle(12, 68, 250, 36) });
        Choices.Items.Add("Existing interactive popup");
        Root.BringToFront();
    }

    internal void Begin()
    {
        Editor.Select();
        Check(Editor.Selected, "editor-focus-established");
        var client = form.TextInputClient;
        Check(client is not null, "parent-text-input-established");
        Tip.Show("Explanation without activation", Editor, 0, 130);
        Check(ReferenceEquals(client, form.TextInputClient) && Editor.Selected, "passive-focus-and-ime-preserved");
        Check(Content.AccessibilityObject.ControlType == AccessibleControlType.ToolTip &&
            Content.AccessibilityObject.Name == "Passive help. Explanation without activation", "canonical-tooltip-semantics");
        output("TOOLTIP:BEGIN");
    }

    internal void VerifySettled()
    {
        Check(Settled && Content.TranslationY == 0 && Content.Opacity == 1, "settled-presentation");
        var area = Content.PaddedClientRectangle;
        var title = TextMeasurer.MeasureText(Tip.ToolTipTitle, Theme.UIFontBold,
            Content.LogicalToDeviceUnits(Theme.FontSize), new Size(area.Width, int.MaxValue), FontStyle.Bold);
        int titleHeight = Math.Max(Content.LogicalToDeviceUnits(Tip.MinimumTextLineHeight), (int)Math.Ceiling(title.Height));
        var body = TextMeasurer.MeasureText("Explanation without activation", Theme.UIFont,
            Content.LogicalToDeviceUnits(Theme.FontSize), new Size(area.Width, int.MaxValue));
        Check(Math.Ceiling(body.Height) <= area.Height - titleHeight - Content.LogicalToDeviceUnits(Tip.TitleSpacing),
            "actual-device-text-fits-popup-clip");
        output("TOOLTIP:SETTLED");
    }

    internal void ExercisePassiveMenu()
    {
        Tip.UseAnimation = false;
        Tip.UseFading = false;
        Tip.Hide(Editor);
        Choices.DroppedDown = true;
        var activeProperty = typeof(Application).GetProperty("ActivePopupWindow", BindingFlags.Static | BindingFlags.NonPublic)!;
        var active = activeProperty.GetValue(null);
        Check(active is PopupWindow, "interactive-popup-established");
        var client = form.TextInputClient;
        Tip.Show("Help beside a list", Editor, 0, 130);
        Check(Choices.DroppedDown && ReferenceEquals(active, activeProperty.GetValue(null)), "interactive-popup-lease-preserved");
        Check(ReferenceEquals(client, form.TextInputClient), "menu-text-input-lease-preserved");
        Tip.Hide(Editor);
        Check(Choices.DroppedDown && ReferenceEquals(active, activeProperty.GetValue(null)), "hide-preserves-interactive-popup");
        Choices.DroppedDown = false;
        Editor.Select();
        output($"TOOLTIP:PASS:{assertions}");
    }

    internal void BeginExit() => Tip.Hide(Editor);

    private void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("ToolTip certification: " + category);
        assertions++;
        output("TOOLTIP:CHECK:" + category);
    }

    public void Dispose()
    {
        try { Tip.Dispose(); }
        finally { owner.Remove(Root); Root.Dispose(); }
    }
}
