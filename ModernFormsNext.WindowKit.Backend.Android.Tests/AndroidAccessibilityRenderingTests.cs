using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidAccessibilityRenderingTests
{
    [Fact]
    public void AttachedSessionSettlesNotificationsWhileRenderingOffscreenEditors()
    {
        using var root = new Panel { AutoScroll = true };
        for (int index = 0; index < 36; index++)
            root.Controls.Add(new Label
            {
                Text = "Synthetic host status with a longer diagnostic description",
                Bounds = new(24, 24 + index * 30, 345, 24)
            });
        Control[] editors =
        [
            new TextBox { Text = "Synthetic single line" },
            new TextBox { MultiLine = true, Text = "Synthetic first line\nSecond line\nThird line", ScrollBars = ScrollBars.Vertical },
            new RichTextBox { Text = "Synthetic rich text\nSecond line", MultiLine = true, ScrollBars = RichTextBoxScrollBars.Vertical },
            new MarkdownEditor { Markdown = "# Synthetic title\n\n**Synthetic bold** and plain text", ShowToolbar = false,
                ViewMode = MarkdownEditorViewMode.Editor, ScrollBars = RichTextBoxScrollBars.Vertical }
        ];
        for (int index = 0; index < editors.Length; index++)
        {
            editors[index].Bounds = new(24, 1150 + index * 160, 345, 112);
            root.Controls.Add(editors[index]);
        }
        using var surface = new SkiaControlSurface(root);
        using var session = new AndroidAccessibilitySession(surface);
        int notifications = 0;
        session.EventsPending += () =>
        {
            if (++notifications > 10000)
                throw new InvalidOperationException("Accessibility notifications did not settle during rendering.");
        };
        session.Attach();
        surface.Resize(411, 840);
        using var bitmap = new SKBitmap(411, 840);
        using var canvas = new SKCanvas(bitmap);
        surface.Render(canvas);
        canvas.Flush();
        var events = session.DrainEvents();
        Assert.NotEmpty(events);
        Assert.InRange(session.CachedNodeCount, 1, 100);
        int before = notifications;
        surface.Render(canvas);
        canvas.Flush();
        Assert.Equal(before, notifications);
        Assert.Empty(session.DrainEvents());
        Assert.All(editors, editor => Assert.True(editor.Top > root.Height));
    }
}
