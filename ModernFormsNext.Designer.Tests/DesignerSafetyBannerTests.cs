using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Services;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerSafetyBannerTests
{
    [Theory]
    [InlineData((int)DesignerPersistenceNoticeKind.RecoveryAvailable, (int)DesignerPersistenceActions.Keep, "Keep Recovery")]
    [InlineData((int)DesignerPersistenceNoticeKind.RecoveryConflict, (int)DesignerPersistenceActions.Discard, "Discard Recovery")]
    [InlineData((int)DesignerPersistenceNoticeKind.ExternalDesignConflict, (int)DesignerPersistenceActions.Keep, "Keep Designer")]
    public void PersistenceActionsNameTheVersionTheyAffect(int rawNoticeKind, int rawAction, string expected)
    {
        var text = DesignerSafetyBanner.GetActionText(
            (DesignerPersistenceNoticeKind)rawNoticeKind,
            (DesignerPersistenceActions)rawAction);

        Assert.Equal(expected, text);
    }
}
