using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using System.Text;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerEventBindingTests
{
    [Fact]
    public void CreateUndoRedoUnbindAndRoundTripPreserveUserMethodsAndOneSubscription()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        var workflow = fixture.Workflow;
        Assert.True(await workflow.BindAsync(fixture.Click, "button1_Click", true, true, CancellationToken.None));
        var sourceWithMethod = fixture.Host.Source;
        Assert.Equal("button1_Click", fixture.Button.Events["Click"]);
        Assert.Contains("private void button1_Click", sourceWithMethod);
        Assert.Equal(1, fixture.Host.Insertions);
        Assert.Equal(sourceWithMethod, fixture.Host.NavigationText);
        Assert.True(fixture.Host.NavigationOffset > 0);
        Assert.True(fixture.Session.Transactions.Undo());
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.Equal(sourceWithMethod, fixture.Host.Source);
        Assert.True(fixture.Session.Transactions.Redo());
        Assert.Equal("button1_Click", fixture.Button.Events["Click"]);
        Assert.True(await workflow.BindAsync(fixture.Click, "button1_Click", false, true, CancellationToken.None));
        Assert.Equal(1, fixture.Host.Insertions);
        var reopened = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(fixture.Session.Document));
        var code = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(code.Succeeded);
        Assert.Equal(1, code.Code.Split("this.button1.Click += this.button1_Click;").Length - 1);
        Assert.True(await workflow.BindAsync(fixture.Click, string.Empty, false, false, CancellationToken.None));
        Assert.True(string.IsNullOrEmpty(fixture.Button.Events["Click"]));
        Assert.Equal(sourceWithMethod, fixture.Host.Source);
        Assert.DoesNotContain("button1.Click +=", new CSharpDesignerGenerator().Generate(fixture.Session.Document).Code);
    });

    [Theory]
    [InlineData("namespace Other; public class Sample { }")]
    [InlineData("namespace Example; public class Sample { private int button1_Click; }")]
    public void InvalidSourceDoesNotCommitBindingOrHistory(string source)
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Host.Source = source;
        Assert.False(await fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, false, CancellationToken.None));
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Transactions.CanUndo);
        Assert.Equal(source, fixture.Host.Source);
        Assert.Equal(0, fixture.Host.Insertions);
    });

    [Fact]
    public void HostDenialPreservesBindingAndUnsavedSource()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Host.Source += " // unsaved note";
        var original = fixture.Host.Source;
        fixture.Host.AllowInsert = false;
        Assert.False(await fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, false, CancellationToken.None));
        Assert.Equal(original, fixture.Host.Source);
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Transactions.CanUndo);
    });

    [Fact]
    public void SelectionChangeWhileReadingCancelsBeforeSourceOrModelWrite()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Host.ReadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, true, CancellationToken.None);
        fixture.Session.SelectForm();
        Assert.False(await operation);
        Assert.Equal(0, fixture.Host.Insertions);
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Transactions.CanUndo);
    });

    [Fact]
    public void LateSelectionChangeRetainsCreatedMethodWithoutBindingToAnotherObject()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Host.AfterInsert = fixture.Session.SelectForm;
        Assert.False(await fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, true, CancellationToken.None));
        Assert.Equal(1, fixture.Host.Insertions);
        Assert.Contains("button1_Click", fixture.Host.Source);
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Document.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Transactions.CanUndo);
        Assert.Null(fixture.Host.NavigationText);
    });

    [Fact]
    public void StaleEventRowCannotBindTheOldObjectAfterSelectionChanges()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        var descriptor = fixture.Click;
        fixture.Session.SelectForm();
        Assert.False(await fixture.Workflow.BindAsync(descriptor, "button1_Click", true, true, CancellationToken.None));
        Assert.Equal(0, fixture.Host.Insertions);
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Document.Events.ContainsKey("Click"));
        Assert.False(fixture.Session.Transactions.CanUndo);
    });

    [Fact]
    public void CommitCallbackSelectionChangePreventsLateCodeNavigation()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Session.Transactions.TransactionCommitted += (_, _) => fixture.Session.SelectForm();
        Assert.True(await fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, true, CancellationToken.None));
        Assert.Equal("button1_Click", fixture.Button.Events["Click"]);
        Assert.Null(fixture.Session.SelectedNode);
        Assert.Null(fixture.Host.NavigationText);
    });

    [Fact]
    public void ActivePointerTransactionPreventsAnEventEditFromJoiningItsUndoUnit()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        using var gesture = fixture.Session.Transactions.Begin("Pointer gesture");
        Assert.False(await fixture.Workflow.BindAsync(fixture.Click, "button1_Click", true, true, CancellationToken.None));
        Assert.Equal(0, fixture.Host.Insertions);
        Assert.False(fixture.Button.Events.ContainsKey("Click"));
        gesture.Rollback();
        Assert.False(fixture.Session.Transactions.CanUndo);
    });

    [Fact]
    public void MissingRenamedHandlerIsDiagnosedWithoutCreatingAnotherMethod()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Button.Events["Click"] = "OldName";
        var source = fixture.Host.Source;
        Assert.False(await fixture.Workflow.BindAsync(fixture.Click, "OldName", false, true, CancellationToken.None));
        Assert.Equal("OldName", fixture.Button.Events["Click"]);
        Assert.Equal(source, fixture.Host.Source);
        Assert.Equal(0, fixture.Host.Insertions);
        // Clearing a broken binding works even when source is unavailable.
        fixture.Host.ReadAvailable = false;
        Assert.True(await fixture.Workflow.BindAsync(fixture.Click, string.Empty, false, false, CancellationToken.None));
    });

    [Fact]
    public void PickerUsesCompatibleInstanceMethodsFromThePairedRootOnly()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        using var fixture = new Fixture();
        fixture.Host.Source = "using Args = System.EventArgs; namespace Example; public class Other { private void OtherMethod(object s, Args e) { } } "
            + "public partial class Sample { private void Compatible(object? sender, Args args) { } "
            + "private void Wrong(int n) { } private static void Static(object? s, Args e) { } private int WrongReturn(object? s, Args e) => 1; }";
        var methods = await fixture.Files.ReadCompatibleEventHandlersAsync(fixture.Session.Document, fixture.Click.Parameters, CancellationToken.None);
        Assert.Equal(new[] { "Compatible" }, methods);
        Assert.Equal(0, fixture.Host.Insertions);
    });

    [Fact]
    public void DefaultEventSelectionUsesTheExistingSingleSelectionGrid()
    {
        using var fixture = new Fixture();
        var grid = new DesignerPropertyGridState(fixture.Session);
        Assert.Equal("Click", grid.SelectDefaultEvent()!.Name);
        fixture.Session.SelectForm();
        Assert.Equal("Load", grid.SelectDefaultEvent()!.Name);
        Assert.Equal(DesignerPropertyGridMode.Events, grid.Mode);
    }

    [Fact]
    public void StandaloneInsertionPreservesUtf8BomAndChecksSourceRevision()
        => DesignerEventTestDispatcher.Run(async () =>
    {
        var directory = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-events-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var environment = new DiskHost(IOPath.Combine(directory, "Sample.mfdesign"));
            var files = new DesignerFileService(environment);
            var path = IOPath.Combine(directory, "Sample.cs");
            File.WriteAllText(path, "// user note\r\nnamespace Example;\r\npublic partial class Sample { }\r\n", new UTF8Encoding(true));
            var document = new DesignDocument { Namespace = "Example", ClassName = "Sample", FormName = "Sample" };
            var prepared = await files.PrepareEventHandlerAsync(document, "OnClick", "object? sender, global::System.EventArgs e", true, CancellationToken.None);
            Assert.True(prepared.Plan.Succeeded, prepared.Plan.Message);
            Assert.True(await files.ApplyEventHandlerAsync(prepared, CancellationToken.None));
            Assert.True(File.ReadAllBytes(path).AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.Contains("// user note\r\n", File.ReadAllText(path));
            var stale = await files.PrepareEventHandlerAsync(document, "OnSecondClick", "object? sender, global::System.EventArgs e", true, CancellationToken.None);
            File.AppendAllText(path, "// later user change\r\n");
            var changed = File.ReadAllBytes(path);
            Assert.False(await files.ApplyEventHandlerAsync(stale, CancellationToken.None));
            Assert.Equal(changed, File.ReadAllBytes(path));
            Assert.Null(new DesignerFileService().GetFormCodePath(document));
        }
        finally { Directory.Delete(directory, recursive: true); }
    });

    [Fact]
    public void LastMomentExternalSaveIsRestoredAndBothVersionsAreRetained()
    {
        var directory = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-event-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var destination = IOPath.Combine(directory, "Sample.cs");
            var staged = IOPath.Combine(directory, "insertion.tmp");
            var original = Encoding.UTF8.GetBytes("// original user source");
            const string external = "// later user save";
            const string inserted = "// original user source with a new handler";
            File.WriteAllText(destination, external, new UTF8Encoding(false));
            File.WriteAllText(staged, inserted, new UTF8Encoding(false));
            var failure = Assert.Throws<IOException>(() => new DesignerEventSourceCommitter(original).Commit(staged, destination));
            Assert.Contains("changed concurrently", failure.Message);
            Assert.Equal(external, File.ReadAllText(destination));
            var retained = Assert.Single(Directory.EnumerateFiles(directory, ".mfn-event-conflict-*.tmp"));
            Assert.Equal(inserted, File.ReadAllText(retained));
            Assert.False(File.Exists(staged));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly FakeHost Host = new();
        internal readonly DesignerSession Session;
        internal readonly DesignerFileService Files;
        internal readonly DesignerEventBindingService Workflow;
        internal readonly DesignControlNode Button = new() { TypeName = "ModernFormsNext.Button", Name = "button1", Bounds = new(10, 10, 100, 30) };
        internal DesignerEventDescriptor Click => new DesignerPropertyGridState(Session).Events.Single(e => e.Name == "Click");

        internal Fixture()
        {
            Session = new DesignerSession(Host);
            var document = new DesignDocument { Namespace = "Example", ClassName = "Sample", FormName = "Sample" };
            document.Controls.Add(Button);
            Session.OpenDocument(document, Host.CurrentDocumentPath);
            Session.SelectNode(Button);
            Files = new DesignerFileService(Host, () => Session.CurrentDocumentPath);
            Workflow = new(Session, Files);
        }
        public void Dispose() => Session.Dispose();
    }

    private sealed class DiskHost(string path) : IDesignerHostEnvironment
    {
        public string? CurrentDocumentPath => path;
        public string? CurrentProjectPath => null;
        public void ReportStatus(string message) { }
        public void ReportOutput(string message) { }
    }

    private sealed class FakeHost : IDesignerHostEnvironment, IDesignerCodeDocumentHostEnvironment
    {
        public string? CurrentDocumentPath { get; } = IOPath.Combine(IOPath.GetTempPath(), "ModernFormsNext-event-buffer", "Sample.mfdesign");
        public string? CurrentProjectPath => null;
        internal string Source = "namespace Example; public partial class Sample : ModernFormsNext.Form { }";
        internal bool AllowInsert = true;
        internal bool ReadAvailable = true;
        internal int Insertions;
        internal string? NavigationText;
        internal int NavigationOffset;
        internal Action? AfterInsert;
        internal TaskCompletionSource<string?>? ReadGate;
        public void ReportStatus(string message) { }
        public void ReportOutput(string message) { }
        public async Task<string?> ReadCodeDocumentAsync(string path, CancellationToken cancellationToken)
            => ReadGate is null ? ReadAvailable ? Source : null : await ReadGate.Task.WaitAsync(cancellationToken);
        public Task<bool> InsertCodeAsync(string path, string expectedText, int insertionOffset, string insertedText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AllowInsert || Source != expectedText) return Task.FromResult(false);
            Source = Source.Insert(insertionOffset, insertedText);
            Insertions++;
            AfterInsert?.Invoke();
            return Task.FromResult(true);
        }
        public Task<bool> NavigateToCodeAsync(string path, string expectedText, int offset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            NavigationText = expectedText; NavigationOffset = offset;
            return Task.FromResult(Source == expectedText && offset >= 0 && offset <= Source.Length);
        }
    }
}
