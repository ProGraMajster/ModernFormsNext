using System.Drawing;
using System.Reflection;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class SuggestionPopupHostTests
{
    [Fact]
    public void PassiveSuggestionPopupKeepsTheParentEditorAndCompositionCurrent()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var text = form.Controls.Add(new TextBox());
        var window = host.Show(form, 320, 240);
        Assert.True(window.Input.Focus(text));
        var client = Assert.IsAssignableFrom<WindowKit.Input.ITextInputClient>(form.TextInputClient);
        Assert.True(client.SetComposingText("provisional"));
        using var popup = Passive(form);
        popup.Controls.Add(new CollectionView { Dock = DockStyle.Fill, ItemsSource = new[] { "one", "two" } });
        popup.Show(text, 0, text.Height);
        Assert.Same(client, form.TextInputClient);
        Assert.True(client.GetState()!.HasComposition);
        Assert.Null(popup.TextInputClient);
        popup.SetTextInputActive(true);
        Assert.Null(popup.TextInputClient);
        Assert.Same(client, form.TextInputClient);
        Assert.True(client.CommitText("committed"));
        Assert.Equal("committed", text.Text);
        popup.Hide();
        Assert.Null(window.ActivePopup);
        Assert.Same(client, form.TextInputClient);
        Assert.True(client.CommitText("!"));
        Assert.Equal("committed!", text.Text);
    }

    [Fact]
    public void HidingOldPassivePopupCannotRetireANewerEditablePopup()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var text = form.Controls.Add(new TextBox());
        var window = host.Show(form, 320, 240);
        Assert.True(window.Input.Focus(text));
        using var first = Passive(form);
        first.Show(0, 30);
        using var second = new PopupWindow(form) { Size = new Size(160, 60) };
        var popupText = second.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        second.Show(0, 30);
        var secondHost = Assert.IsType<TestPopupHost>(window.ActivePopup);
        Assert.True(secondHost.Input.Focus(popupText));
        var client = second.TextInputClient;
        Assert.NotNull(client);
        first.Hide();
        Assert.Same(secondHost, window.ActivePopup);
        Assert.Same(client, second.TextInputClient);
        Assert.Null(form.TextInputClient);
        second.Hide();
        Assert.NotNull(form.TextInputClient);
        Assert.Null(window.ActivePopup);
    }

    [Fact]
    public void PassiveReplacementReacquiresParentAndOldEditableHidePreservesIt()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var text = form.Controls.Add(new TextBox());
        var window = host.Show(form, 320, 240);
        Assert.True(window.Input.Focus(text));
        using var first = new PopupWindow(form) { Size = new Size(160, 60) };
        var popupText = first.Controls.Add(new TextBox { Dock = DockStyle.Fill });
        first.Show(0, 30);
        Assert.True(Assert.IsType<TestPopupHost>(window.ActivePopup).Input.Focus(popupText));
        Assert.Null(form.TextInputClient);
        using var second = Passive(form);
        second.Show(0, 30);
        var client = form.TextInputClient;
        Assert.NotNull(client);
        Assert.Null(first.TextInputClient);
        first.Hide();
        Assert.Same(second, window.ActivePopup!.Window);
        Assert.Same(client, form.TextInputClient);
        Assert.True(client.CommitText("parent"));
        Assert.Equal("parent", text.Text);
    }

    [Fact]
    public void HideObserverOpeningNewPassivePopupKeepsTheNewActiveSession()
    {
        using var host = ModernFormsTestHost.Create();
        var form = new Form { UseSystemDecorations = true };
        var text = form.Controls.Add(new TextBox());
        var window = host.Show(form, 320, 240);
        Assert.True(window.Input.Focus(text));
        var client = form.TextInputClient;
        using var first = Passive(form);
        using var second = Passive(form);
        first.VisibleChanged += (_, _) => { if (!first.Visible) second.Show(0, 30); };
        first.Show(0, 30);
        first.Hide();
        Assert.Same(second, window.ActivePopup!.Window);
        Assert.Same(client, form.TextInputClient);
    }

    private static PopupWindow Passive(Form form)
    {
        var popup = new PopupWindow(form) { Size = new Size(160, 80) };
        // Exercise the internal built-in presenter profile without expanding the production
        // friend-assembly or public PopupWindow API solely for a fixture.
        typeof(PopupWindow).GetProperty("RetainParentTextInput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(popup, true);
        return popup;
    }
}
