using ModernFormsNext.Animations;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ToolTipPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InactiveOwnerRequiresShowAlways(bool showAlways)
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        window.SetActive(false);
        using var tip = ImmediateTip();
        tip.ShowAlways = showAlways;
        tip.Show("Inactive owner", anchor);
        Assert.Equal(showAlways, window.Popups.Any(popup => popup.IsVisible));
        Assert.False(form.IsActive);
    }

    [Fact]
    public void ShowAlwaysKeepsTheVisibleInactiveOwnersPassivePopup()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        using var tip = ImmediateTip();
        tip.ShowAlways = true;
        tip.Show("Retain while inactive", anchor);
        var popup = Assert.Single(window.Popups);
        window.SetActive(false);
        Assert.True(popup.IsVisible);
        Assert.False(form.IsActive);
    }

    [Fact]
    public void PassiveToolTipPreservesTheParentsNativeTextInputClient()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var editor = form.Controls.Add(new TextBox());
        using var window = host.Show(form);
        window.Input.Focus(editor);
        var client = Assert.IsAssignableFrom<ModernFormsNext.WindowKit.Input.ITextInputClient>(form.TextInputClient);
        using var tip = ImmediateTip();
        tip.Show("Editor help", editor);
        Assert.Same(client, form.TextInputClient);
        Assert.Same(editor, window.FocusedControl);
        tip.Hide(editor);
        Assert.Same(client, form.TextInputClient);
    }

    [Fact]
    public void PassiveToolTipDoesNotReplaceAnInteractivePopup()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var combo = form.Controls.Add(new ComboBox());
        combo.Items.Add("First");
        using var window = host.Show(form);
        combo.DroppedDown = true;
        var interactive = Assert.IsType<TestPopupHost>(window.ActivePopup);
        using var tip = ImmediateTip();
        tip.Show("Choice help", combo);
        Assert.Same(interactive, window.ActivePopup);
        Assert.True(interactive.IsVisible);
        tip.Hide(combo);
        Assert.Same(interactive, window.ActivePopup);
    }

    [Fact]
    public void DisposingFromPopupCallbackCannotResurrectPresentation()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        using var tip = ImmediateTip();
        tip.Popup += (_, _) => tip.Dispose();
        tip.Show("Retired", anchor);
        Assert.DoesNotContain(window.Popups, popup => popup.IsVisible);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    [Fact]
    public void AManualAnchorDisposalRetiresItsPopup()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        using var tip = ImmediateTip();
        tip.Show("Manual help", anchor);
        var popup = Assert.Single(window.Popups);
        anchor.Dispose();
        Assert.False(popup.IsVisible);
    }

    [Fact]
    public void HidingFromThePopupCallbackCancelsTheInFlightFirstShow()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form();
        var anchor = form.Controls.Add(new Button());
        using var window = host.Show(form);
        using var tip = ImmediateTip();
        tip.Popup += (_, _) => tip.Hide(anchor);
        tip.Show("Cancelled by callback", anchor);
        Assert.DoesNotContain(window.Popups, popup => popup.IsVisible);
        Assert.Equal(0, AnimationScheduler.Default.GetDiagnostics().ActiveAnimationCount);
    }

    private static ToolTip ImmediateTip() => new() { UseAnimation = false, UseFading = false };
}
