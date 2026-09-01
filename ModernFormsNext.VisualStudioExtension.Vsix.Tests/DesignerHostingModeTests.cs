using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using ModernFormsNext.VisualStudioExtension.Commands;
using ModernFormsNext.VisualStudioExtension.Editors;
using ModernFormsNext.VisualStudioExtension.Options;
using Xunit;

namespace ModernFormsNext.VisualStudioExtension.Vsix.Tests;

public sealed class DesignerHostingModeTests
{
    [Fact]
    public void DesignerOptionDefaultsToIntegrated()
    {
        var property = TypeDescriptor.GetProperties(typeof(DesignerOptionsPage))[
            nameof(DesignerOptionsPage.HostingMode)];
        var defaultValue = (DefaultValueAttribute?)property?.Attributes[typeof(DefaultValueAttribute)];

        Assert.NotNull(defaultValue);
        Assert.Equal(DesignerHostingMode.Integrated, defaultValue!.Value);
    }

    [Fact]
    public void StandaloneOptionRoundTripsThroughTheVisualStudioPropertyConverter()
    {
        var converter = new DesignerHostingModeTypeConverter();
        var persistedValue = converter.ConvertToInvariantString(DesignerHostingMode.Standalone);
        var reloaded = (DesignerHostingMode)converter.ConvertFromInvariantString(persistedValue!)!;

        Assert.Equal("Separate window", persistedValue);
        Assert.Equal(DesignerHostingMode.Standalone, reloaded);
        Assert.Equal(
            "Integrated in Visual Studio",
            converter.ConvertToInvariantString(DesignerHostingMode.Integrated));
    }

    [Fact]
    public void ExistingPaneKeepsItsCapturedModeAndNextOpenUsesTheNewSelection()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"ModernFormsNext-HostingMode-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var integrated = CreatePane(
                Path.Combine(directory, "Integrated.mfdesign"),
                DesignerHostingMode.Integrated);

            var selectedMode = DesignerHostingMode.Standalone;
            using var standalone = CreatePane(
                Path.Combine(directory, "Standalone.mfdesign"),
                selectedMode);

            Assert.Equal(DesignerHostingMode.Integrated, integrated.HostingMode);
            Assert.Equal(DesignerHostingMode.Standalone, standalone.HostingMode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(DesignerHostingMode.Integrated)]
    [InlineData(DesignerHostingMode.Standalone)]
    public void RegisteredEditorFactoryRoutesNewDesignerOpenToSelectedMode(
        DesignerHostingMode selectedMode)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"ModernFormsNext-HostingModeRouting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new MfDesignEditorFactory(
                new EmptyServiceProvider(),
                () => selectedMode);
            using var pane = factory.CreateEditorPane(
                Path.Combine(directory, "Routed.mfdesign"));

            Assert.Equal(selectedMode, pane.HostingMode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(DesignerHostingMode.Integrated, "--host-mode integrated", true)]
    [InlineData(DesignerHostingMode.Standalone, "--host-mode standalone", false)]
    public void LauncherUsesExplicitModeAndOnlyIntegratedCarriesAParentHandle(
        DesignerHostingMode mode,
        string expectedModeArgument,
        bool expectsParent)
    {
        var arguments = OutOfProcessDesignerHostControl.BuildHostArguments(
            "C:\\Project Files\\Form1.mfdesign",
            "C:\\Project Files\\Project.csproj",
            "pipe-name",
            mode,
            new IntPtr(12345),
            67890);

        Assert.Contains(expectedModeArgument, arguments, StringComparison.Ordinal);
        Assert.Contains("--owner-process 67890", arguments, StringComparison.Ordinal);
        Assert.Equal(expectsParent, arguments.Contains("--parent-window 12345"));
    }

    private static MfDesignEditorPane CreatePane(string path, DesignerHostingMode mode)
        => new(
            new EmptyServiceProvider(),
            path,
            new FakeDocumentHost(),
            new FakeDocumentServices(),
            mode);

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FakeDocumentHost : IDesignerDocumentHost, IWin32Window
    {
        public event EventHandler<DesignerDocumentDirtyChangedEventArgs>? DocumentDirtyChanged
        {
            add { }
            remove { }
        }

        public IWin32Window Window => this;

        public IntPtr Handle => IntPtr.Zero;

        public bool TryOpenDocument(string path) => true;

        public DesignerHostSaveResult SaveDocument() => DesignerHostSaveResult.Saved;

        public bool TryDiscardDocumentRecovery() => true;

        public void PostToOwnerThread(Action action) => action();

        public void Dispose()
        {
        }
    }

    private sealed class FakeDocumentServices : IVisualStudioDocumentServices
    {
        public int QuerySaveFile(string documentPath, out uint result)
        {
            result = (uint)tagVSQuerySaveResult.QSR_SaveOK;
            return VSConstants.S_OK;
        }

        public int SaveDocDataToFile(
            VSSAVEFLAGS saveFlags,
            object persistFile,
            string documentPath,
            out string newDocumentPath,
            out int saveCanceled)
        {
            newDocumentPath = documentPath;
            saveCanceled = 0;
            return VSConstants.S_OK;
        }

        public void UpdateDirtyState(uint documentCookie)
        {
        }

        public RunningDocumentState GetRunningDocumentState(uint documentCookie, string documentPath)
            => new(documentCookie, documentCookie, isCookieValid: true, isDirty: false);

        public void ReportSaveCanceled(string message)
        {
        }
    }
}
