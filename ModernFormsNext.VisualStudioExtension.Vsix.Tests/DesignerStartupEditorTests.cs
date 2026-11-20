using System;
using Microsoft.VisualStudio;
using Xunit;

namespace ModernFormsNext.VisualStudioExtension.Vsix.Tests;

public sealed class DesignerStartupEditorTests
{
    [Fact]
    public void RequestedFactoryAndViewMatchInstalledPackageRegistration()
    {
        Assert.Equal(new Guid(ModernFormsDesignerPackage.EditorFactoryGuidString), DesignerStartupVisualStudio.EditorFactory);
        Assert.Equal(new Guid(ModernFormsDesignerPackage.PackageGuidString), DesignerStartupVisualStudio.DesignerPackage);
        Assert.Equal(VSConstants.LOGVIEWID_Designer, DesignerStartupVisualStudio.DesignerView);
    }

    [Fact]
    public void JsonTextFrameCannotSatisfyDesignerSelection()
    {
        var textFactory = new Guid("90A6B3A7-C1A3-4009-A288-E2FF89E96FA0");
        Assert.Throws<InvalidOperationException>(() => DesignerStartupVisualStudio.ValidateFrame(
            textFactory, @"C:\Fixture\PreviewExample.mfdesign", @"C:\Fixture\PreviewExample.mfdesign"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(@"C:\Other\PreviewExample.mfdesign")]
    public void DifferentDocumentCannotSatisfyDesignerSelection(string? actualDocument)
    {
        Assert.Throws<InvalidOperationException>(() => DesignerStartupVisualStudio.ValidateFrame(
            DesignerStartupVisualStudio.EditorFactory, actualDocument, @"C:\Fixture\PreviewExample.mfdesign"));
    }

    [Fact]
    public void MatchingDesignerFrameAcceptsWindowsPathCasing()
    {
        DesignerStartupVisualStudio.ValidateFrame(DesignerStartupVisualStudio.EditorFactory,
            @"c:\fixture\PreviewExample.mfdesign", @"C:\Fixture\PreviewExample.mfdesign");
    }
}
