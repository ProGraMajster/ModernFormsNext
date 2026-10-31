using System.ComponentModel;
using System.Drawing;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class WindowRightToLeftTests
{
    [Fact]
    public void FormForwardsDirectionThroughItsCanonicalAdapterAndUsesTheWindowEventSender()
    {
        using var host = ModernFormsTestHost.Create();
        using var form = new Form { ClientSize = new Size(320, 180), UseSystemDecorations = true };
        var descriptor = TypeDescriptor.GetProperties(form)[nameof(WindowBase.RightToLeft)]!;
        Assert.False(descriptor.ShouldSerializeValue(form));
        var flow = form.Controls.Add(new FlowLayoutPanel { Dock = DockStyle.Fill });
        var first = flow.Controls.Add(new Button { Size = new Size(70, 30), TabIndex = 0 });
        var second = flow.Controls.Add(new Button { Size = new Size(70, 30), TabIndex = 1 });
        object? sender = null;
        form.RightToLeftChanged += (source, _) => sender = source;
        form.Show();
        form.RightToLeft = RightToLeft.Yes;
        Assert.Same(form, sender);
        Assert.Equal(RightToLeft.Yes, flow.RightToLeft);
        Assert.True(first.Left > second.Left);
        Assert.True(form.UseSystemDecorations);
        descriptor.ResetValue(form);
        Assert.Equal(RightToLeft.No, flow.RightToLeft);
        Assert.True(first.Left < second.Left);
        Assert.False(descriptor.ShouldSerializeValue(form));
    }
}
