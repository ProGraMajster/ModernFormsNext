using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ValidationTraversalTests
{
    [Fact]
    public void TraversalExcludesImplicitFrameworkControlsAndTheirSubtrees()
    {
        using var root = new Panel();
        var implicitControl = root.Controls.AddImplicitControl(new Panel());
        var internalChild = implicitControl.Controls.Add(new TextBox());
        var field = root.Controls.Add(new TextBox());
        int fields = 0;
        implicitControl.Validating += (_, _) => Assert.Fail("Implicit control was validated.");
        internalChild.Validating += (_, _) => Assert.Fail("Implicit subtree was validated.");
        field.Validating += (_, _) => fields++;
        Assert.True(root.ValidateChildren());
        Assert.Equal(1, fields);
    }
}
