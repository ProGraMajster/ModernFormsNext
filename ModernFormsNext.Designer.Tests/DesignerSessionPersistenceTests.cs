using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerSessionPersistenceTests
{
    [Fact]
    public void ActiveUnsavedDocumentDoesNotInheritEnvironmentDocumentPath()
    {
        var originalPath = IOPath.GetFullPath("ExistingForm.mfdesign");
        var environment = new MutableDesignerEnvironment
        {
            CurrentDocumentPath = originalPath,
            CurrentProjectPath = IOPath.GetFullPath("Example.csproj")
        };
        using var session = new DesignerSession(environment);
        session.LoadDocument(CreateDocument("ExistingForm", "existingForm", "existingButton"));

        session.NewDocument();

        Assert.Null(session.CurrentDocumentPath);
        Assert.Null(session.ActiveOpenDocument?.Path);
        Assert.Equal(originalPath, session.OpenDocuments[0].Path);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void FileServiceTreatsNullActivePathProviderAsAuthoritativeForUnsavedTab()
    {
        var originalPath = IOPath.GetFullPath("ExistingForm.mfdesign");
        var environment = new MutableDesignerEnvironment
        {
            CurrentDocumentPath = originalPath,
            CurrentProjectPath = IOPath.GetFullPath("Example.csproj")
        };
        using var session = new DesignerSession(environment);
        session.LoadDocument(CreateDocument("ExistingForm", "existingForm", "existingButton"));
        session.NewDocument();
        var files = new DesignerFileService(
            environment,
            currentDocumentPathProvider: () => session.CurrentDocumentPath);

        var designPath = files.GetDesignDocumentPath(session.Document);
        var generatedPath = files.GetGeneratedCodePath(session.Document);

        Assert.NotEqual(originalPath, designPath);
        Assert.NotEqual(
            IOPath.ChangeExtension(originalPath, ".Designer.cs"),
            generatedPath);
        Assert.Equal(IOPath.Combine(AppContext.BaseDirectory, $"{session.Document.ClassName}.mfdesign"), designPath);
        Assert.Equal(IOPath.Combine(AppContext.BaseDirectory, $"{session.Document.ClassName}.Designer.cs"), generatedPath);
    }

    [Fact]
    public void OpenDocumentsKeepIndependentPathsProjectsAndRevisionGenerations()
    {
        var firstPath = IOPath.GetFullPath("FirstForm.mfdesign");
        var secondPath = IOPath.GetFullPath("SecondForm.mfdesign");
        var firstProject = IOPath.GetFullPath(IOPath.Combine("FirstProject", "FirstProject.csproj"));
        var secondProject = IOPath.GetFullPath(IOPath.Combine("SecondProject", "SecondProject.csproj"));
        var environment = new MutableDesignerEnvironment { CurrentProjectPath = firstProject };
        using var session = new DesignerSession(environment);

        session.OpenDocument(CreateDocument("FirstForm", "firstForm", "firstButton"), firstPath);
        environment.CurrentProjectPath = secondProject;
        session.OpenDocument(CreateDocument("SecondForm", "secondForm", "secondButton"), secondPath);

        var first = session.OpenDocuments[0];
        var second = session.OpenDocuments[1];
        Assert.Equal(firstPath, first.Path);
        Assert.Equal(firstProject, first.ProjectPath);
        Assert.Equal(secondPath, second.Path);
        Assert.Equal(secondProject, second.ProjectPath);

        session.SwitchDocument(0);
        session.Transactions.ClearHistory();
        session.SwitchDocument(1);
        session.Transactions.ClearHistory();
        session.Transactions.ClearHistory();

        Assert.Equal(1, first.RevisionGeneration);
        Assert.Equal(2, second.RevisionGeneration);
        Assert.Equal(firstPath, first.Path);
        Assert.Equal(firstProject, first.ProjectPath);
    }

    [Fact]
    public void DocumentTargetedMarkSavedCannotCleanNewerRevisionOrGeneration()
    {
        using var session = CreateSession(out var button);
        var openDocument = Assert.IsType<DesignerOpenDocument>(session.ActiveOpenDocument);

        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("First edit"));
        var firstGeneration = openDocument.RevisionGeneration;
        var firstRevision = openDocument.History.CurrentRevision;
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Second edit"));

        Assert.True(session.MarkSaved(openDocument, firstGeneration, firstRevision));
        Assert.True(session.IsDirty);
        Assert.Equal(firstRevision, openDocument.History.SavedRevision);

        var revisionBeforeGenerationChange = openDocument.History.CurrentRevision;
        session.Transactions.ClearHistory();
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Third edit"));

        Assert.False(session.MarkSaved(openDocument, firstGeneration, revisionBeforeGenerationChange));
        Assert.True(session.IsDirty);
        Assert.NotEqual(firstGeneration, openDocument.RevisionGeneration);
    }

    [Fact]
    public void CleanBaselineReloadClearsHistoryAndPreservesSelectionByName()
    {
        using var session = CreateSession(out var originalButton);
        var openDocument = Assert.IsType<DesignerOpenDocument>(session.ActiveOpenDocument);
        session.SelectNode(originalButton);
        session.SetPropertyValue(originalButton, "Text", DesignPropertyValue.FromString("Local edit"));
        var generationBeforeReload = openDocument.RevisionGeneration;
        var replacement = CreateDocument("ReloadedForm", "reloadedForm", originalButton.Name);
        var replacementButton = replacement.Controls[0];

        session.ReloadDocumentBaseline(openDocument, replacement, markDirty: false, "Reloaded from disk.");

        Assert.Same(replacement, session.Document);
        Assert.Same(replacementButton, session.SelectedNode);
        Assert.NotSame(originalButton, session.SelectedNode);
        Assert.False(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);
        Assert.Equal(generationBeforeReload + 1, openDocument.RevisionGeneration);
    }

    [Fact]
    public void RecoveryBaselineReloadIsDirtyWithoutUndoAndSupportsLaterUndoRedo()
    {
        using var session = CreateSession(out _);
        var openDocument = Assert.IsType<DesignerOpenDocument>(session.ActiveOpenDocument);
        var recovered = CreateDocument("RecoveredForm", "recoveredForm", "recoveredButton");
        var recoveredButton = recovered.Controls[0];

        session.ReloadDocumentBaseline(openDocument, recovered, markDirty: true, "Recovered unsaved work.");

        Assert.True(session.IsDirty);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);

        session.SetPropertyValue(recoveredButton, "Text", DesignPropertyValue.FromString("Edited after recovery"));
        Assert.True(session.Transactions.CanUndo);
        Assert.True(session.Transactions.Undo());
        Assert.False(recoveredButton.Properties.ContainsKey("Text"));
        Assert.True(session.IsDirty);
        Assert.True(session.Transactions.Redo());
        Assert.Equal("Edited after recovery", recoveredButton.Properties["Text"].GetString());
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void BaselineReloadRollsBackModelSelectionAndHistoryWhenHostSelectionObserverThrows()
    {
        using var session = CreateSession(out var originalButton);
        var openDocument = Assert.IsType<DesignerOpenDocument>(session.ActiveOpenDocument);
        var originalDocument = openDocument.Document;
        session.SelectNode(originalButton);
        session.SetPropertyValue(originalButton, "Text", DesignPropertyValue.FromString("Protected local edit"));
        var generation = openDocument.RevisionGeneration;
        var revision = openDocument.History.CurrentRevision;
        var replacement = CreateDocument("ReloadedForm", "reloadedForm", originalButton.Name);
        EventHandler throwingObserver = (_, _) => throw new InvalidOperationException("selection observer failed");
        session.Host.Selection.SelectionChanged += throwingObserver;

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => session.ReloadDocumentBaseline(
                openDocument,
                replacement,
                markDirty: false,
                "Reloaded from disk."));

            Assert.Equal("selection observer failed", exception.Message);
            Assert.Same(originalDocument, openDocument.Document);
            Assert.Same(originalDocument, session.Document);
            Assert.NotSame(replacement, session.Document);
            Assert.Same(originalButton, session.SelectedNode);
            Assert.True(openDocument.History.CanUndo);
            Assert.True(openDocument.History.IsDirty);
            Assert.Equal(revision, openDocument.History.CurrentRevision);
            Assert.Equal(generation, openDocument.RevisionGeneration);
        }
        finally
        {
            session.Host.Selection.SelectionChanged -= throwingObserver;
        }
    }

    [Fact]
    public void CloseDocumentClearsItsHistoryAndAdvancesRevisionGeneration()
    {
        using var session = CreateSession(out var firstButton);
        var closedDocument = Assert.IsType<DesignerOpenDocument>(session.ActiveOpenDocument);
        session.SetPropertyValue(firstButton, "Text", DesignPropertyValue.FromString("Unsaved edit"));
        session.OpenDocument(CreateDocument("SecondForm", "secondForm", "secondButton"), "SecondForm.mfdesign");
        var generationBeforeClose = closedDocument.RevisionGeneration;

        session.CloseDocument(0);

        Assert.DoesNotContain(closedDocument, session.OpenDocuments);
        Assert.False(closedDocument.History.CanUndo);
        Assert.False(closedDocument.History.CanRedo);
        Assert.False(closedDocument.History.IsDirty);
        Assert.Equal(0, closedDocument.History.CurrentRevision);
        Assert.Equal(generationBeforeClose + 1, closedDocument.RevisionGeneration);
    }

    private static DesignerSession CreateSession(out DesignControlNode button)
    {
        var document = CreateDocument("MainForm", "mainForm", "button1");
        button = document.Controls[0];
        var session = new DesignerSession(
            environment: null,
            initialRenderMode: DesignerControlRenderMode.Runtime,
            historyLimit: 500);
        session.OpenDocument(document, path: null);
        return session;
    }

    private static DesignDocument CreateDocument(string className, string formName, string buttonName)
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = className,
            FormName = formName,
            Size = new DesignSize(800, 600)
        };
        document.Controls.AddNode("Button", buttonName, new DesignBounds(10, 10, 100, 30));
        return document;
    }

    private sealed class MutableDesignerEnvironment : IDesignerHostEnvironment
    {
        public string? CurrentDocumentPath { get; set; }

        public string? CurrentProjectPath { get; set; }

        public void ReportStatus(string message)
        {
        }

        public void ReportOutput(string message)
        {
        }
    }
}
