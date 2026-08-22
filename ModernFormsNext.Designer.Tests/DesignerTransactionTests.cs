using System.Runtime.CompilerServices;
using ModernFormsNext;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designing;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerTransactionTests
{
    [Fact]
    public void BeginCommitCreatesOneUndoUnit()
    {
        using var session = CreateSession(out var button);

        using (var transaction = session.Transactions.Begin("Edit button"))
        {
            session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Saved"));
            session.SetNodeBounds(button, new DesignBounds(20, 30, 160, 42));
            transaction.Commit();
        }

        Assert.True(session.Transactions.CanUndo);
        Assert.Equal("Edit button", session.Transactions.UndoDescription);
        Assert.True(session.Transactions.Undo());
        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
    }

    [Fact]
    public void ExplicitRollbackRestoresEveryRecordedChange()
    {
        using var session = CreateSession(out var button);
        using var transaction = session.Transactions.Begin("Failed edit");
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Temporary"));
        session.SetNodeBounds(button, new DesignBounds(90, 80, 170, 60));

        transaction.Rollback();

        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void DisposeWithoutCommitRollsBackAutomatically()
    {
        using var session = CreateSession(out var button);

        using (session.Transactions.Begin("Cancelled edit"))
            session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Temporary"));

        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void NestedCommitJoinsOutermostUndoUnit()
    {
        using var session = CreateSession(out var button);
        using (var outer = session.Transactions.Begin("Edit button"))
        {
            session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("One"));
            using (var nested = session.Transactions.Begin("Nested layout"))
            {
                session.SetNodeBounds(button, new DesignBounds(40, 50, 180, 48));
                nested.Commit();
            }

            outer.Commit();
        }

        Assert.Equal("Edit button", session.Transactions.UndoDescription);
        session.Transactions.Undo();
        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void NestedRollbackLeavesEarlierOuterChangesIntact()
    {
        using var session = CreateSession(out var button);
        using (var outer = session.Transactions.Begin("Outer edit"))
        {
            session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Keep"));
            using (var nested = session.Transactions.Begin("Nested edit"))
            {
                session.SetPropertyValue(button, "Dock", DesignPropertyValue.FromString("Fill"));
                nested.Rollback();
            }

            Assert.False(button.Properties.ContainsKey("Dock"));
            outer.Commit();
        }

        Assert.Equal("Keep", button.Properties["Text"].GetString());
        session.Transactions.Undo();
        Assert.False(button.Properties.ContainsKey("Text"));
    }

    [Theory]
    [InlineData("Text", "Changed")]
    [InlineData("Dock", "Fill")]
    [InlineData("Anchor", "Top, Left, Right")]
    [InlineData("Padding", "1, 2, 3, 4")]
    [InlineData("Margin", "4, 3, 2, 1")]
    [InlineData("Location", "20, 30")]
    [InlineData("Size", "140, 45")]
    public void PropertyGridEditIsOneUndoableUnit(string propertyName, string value)
    {
        using var session = CreateSession(out var button);
        session.SelectNode(button);
        var state = new DesignerPropertyGridState(session);
        var property = FindProperty(state.Properties, propertyName);
        state.SelectRow(new DesignerPropertyGridRow(property));

        Assert.True(state.CommitSelectedValue(value));
        Assert.True(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);

        Assert.True(session.Transactions.Undo());
        Assert.False(session.Transactions.CanUndo);
        Assert.True(session.Transactions.CanRedo);
        Assert.Same(button, session.SelectedNode);
    }

    [Fact]
    public void NoOpPropertyChangeDoesNotCreateHistoryEntry()
    {
        using var session = CreateSession(out var button);

        session.SetNodeBounds(button, button.Bounds);

        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void AddControlUndoRedoRestoresIdentityBoundsSelectionAndSerialization()
    {
        using var session = CreateEmptySession();

        var button = session.AddControl("Button");
        var serialized = DesignDocumentSerializer.Default.Serialize(session.Document);
        Assert.Same(button, session.SelectedNode);

        session.Transactions.Undo();
        Assert.Empty(session.Document.Controls);
        Assert.Null(session.SelectedNode);

        session.Transactions.Redo();
        Assert.Same(button, Assert.Single(session.Document.Controls));
        Assert.Same(button, session.SelectedNode);
        Assert.Equal(serialized, DesignDocumentSerializer.Default.Serialize(session.Document));
    }

    [Fact]
    public void RemoveSubtreeUndoRedoRetainsChildrenPropertiesAndOrder()
    {
        var document = CreateDocument();
        var panel = document.Controls[0];
        var button = panel.Children.AddNode("Button", "buttonNested", new DesignBounds(5, 6, 90, 30));
        var label = panel.Children.AddNode("Label", "labelNested", new DesignBounds(7, 40, 80, 24));
        button.Properties["Text"] = DesignPropertyValue.FromString("Nested");
        using var session = CreateSession(document);
        session.SelectNode(panel);

        Assert.True(session.DeleteNode(panel));
        Assert.Empty(session.Document.Controls);

        session.Transactions.Undo();
        var restored = Assert.Single(session.Document.Controls);
        Assert.Same(panel, restored);
        Assert.Equal([button, label], restored.Children);
        Assert.Equal("Nested", restored.Children[0].Properties["Text"].GetString());
        Assert.Same(panel, session.SelectedNode);

        session.Transactions.Redo();
        Assert.Empty(session.Document.Controls);
    }

    [Fact]
    public void ReorderUndoRedoPreservesExactChildSequence()
    {
        var document = CreateDocument();
        var first = document.Controls[0];
        var second = document.Controls.AddNode("Label", "label1", new DesignBounds(20, 50, 80, 24));
        var third = document.Controls.AddNode("TextBox", "textBox1", new DesignBounds(20, 80, 120, 28));
        using var session = CreateSession(document);
        session.SelectNode(second);

        Assert.True(session.MoveSelectedNodeUp());
        Assert.Equal([second, first, third], document.Controls);
        session.Transactions.Undo();
        Assert.Equal([first, second, third], document.Controls);
        session.Transactions.Redo();
        Assert.Equal([second, first, third], document.Controls);
    }

    [Fact]
    public void ReparentUndoRedoRestoresParentIndexAndLogicalBounds()
    {
        var document = CreateDocument();
        var panel = document.Controls[0];
        panel.TypeName = "Panel";
        panel.Bounds = new DesignBounds(100, 100, 300, 200);
        var button = document.Controls.AddNode("Button", "button2", new DesignBounds(130, 140, 90, 30));
        using var session = CreateSession(document);

        Assert.True(session.MoveNodeToOutlineTarget(button, panel));
        Assert.Same(panel, session.FindParent(button));

        session.Transactions.Undo();
        Assert.Null(session.FindParent(button));
        Assert.Same(button, document.Controls[1]);
        Assert.Equal(new DesignBounds(130, 140, 90, 30), button.Bounds);

        session.Transactions.Redo();
        Assert.Same(panel, session.FindParent(button));
    }

    [Fact]
    public void MoveResizeAndRepeatedDragValuesCoalesceIntoOneUndoUnit()
    {
        using var session = CreateSession(out var button);
        using (var transaction = session.Transactions.Begin("Move button1"))
        {
            for (var index = 1; index <= 100; index++)
                session.SetNodeBounds(button, new DesignBounds(10 + index, 10 + index, 100 + index, 30 + index));
            transaction.Commit();
        }

        Assert.Equal("Move button1", session.Transactions.UndoDescription);
        session.Transactions.Undo();
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
        Assert.False(session.Transactions.CanUndo);
        session.Transactions.Redo();
        Assert.Equal(new DesignBounds(110, 110, 200, 130), button.Bounds);
    }

    [Fact]
    public void RenameUndoRedoRestoresGeneratedFieldIdentity()
    {
        using var session = CreateSession(out var button);

        session.SetNodeName(button, "saveButton");
        Assert.Contains("saveButton", new CSharpDesignerGenerator().Generate(session.Document).Code);
        session.Transactions.Undo();
        Assert.Equal("button1", button.Name);
        session.Transactions.Redo();
        Assert.Equal("saveButton", button.Name);
    }

    [Fact]
    public void CollectionReplacementUndoRedoPreservesOrderAndItems()
    {
        var document = CreateDocument();
        var panel = document.Controls[0];
        panel.TypeName = "Panel";
        var first = panel.Children.AddNode("Label", "first", new DesignBounds(1, 1, 40, 20));
        var second = panel.Children.AddNode("Label", "second", new DesignBounds(1, 25, 40, 20));
        var third = new DesignControlNode { TypeName = "Button", Name = "third", Bounds = new DesignBounds(1, 50, 50, 25) };
        using var session = CreateSession(document);

        session.ReplaceChildren(panel, [second, third, first], "Edit collection");
        Assert.Equal([second, third, first], panel.Children);
        session.Transactions.Undo();
        Assert.Equal([first, second], panel.Children);
        session.Transactions.Redo();
        Assert.Equal([second, third, first], panel.Children);
    }

    [Fact]
    public void ComplexEditorCancelRollsBackWithoutHistory()
    {
        using var session = CreateSession(out var button);
        using var transaction = session.Transactions.Begin("Edit Interaction Effects");
        var snapshot = DesignerModelMutationSnapshot.CaptureNode(button);
        button.Properties["InteractionEffects"] = DesignPropertyValue.FromString("temporary");
        snapshot.RecordChanges(session.Transactions);

        transaction.Rollback();

        Assert.False(button.Properties.ContainsKey("InteractionEffects"));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ComplexEditorOkCreatesOneAtomicHistoryItem()
    {
        using var session = CreateSession(out var button);
        using (var transaction = session.Transactions.Begin("Edit Path Geometry"))
        {
            var snapshot = DesignerModelMutationSnapshot.CaptureNode(button);
            button.Properties["Data"] = DesignPropertyValue.FromStructuredObject(
                "PathGeometry",
                new Dictionary<string, DesignPropertyValue> { ["Figures"] = DesignPropertyValue.FromInt32(2) });
            snapshot.RecordChanges(session.Transactions);
            transaction.Commit();
        }

        Assert.Equal("Edit Path Geometry", session.Transactions.UndoDescription);
        session.Transactions.Undo();
        Assert.False(button.Properties.ContainsKey("Data"));
    }

    [Fact]
    public void NewCommitAfterUndoClearsRedo()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("One"));
        session.Transactions.Undo();
        Assert.True(session.Transactions.CanRedo);

        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Two"));

        Assert.False(session.Transactions.CanRedo);
        Assert.Equal("Two", button.Properties["Text"].GetString());
    }

    [Fact]
    public void SaveMarkerTracksUndoBeforeAndRedoBackToSavedRevision()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Saved"));
        session.MarkSaved();
        Assert.False(session.IsDirty);

        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Later"));
        Assert.True(session.IsDirty);
        session.Transactions.Undo();
        Assert.False(session.IsDirty);
        session.Transactions.Undo();
        Assert.True(session.IsDirty);
        session.Transactions.Redo();
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ClearHistoryReleasesUndoRedoAndPreservesDirtyState()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Changed"));

        session.Transactions.ClearHistory();

        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void HistoryLimitDropsOldestEntriesAndKeepsEvictedSaveMarkerDirty()
    {
        using var session = CreateSession(out var button);
        session.Transactions.HistoryLimit = 2;

        for (var index = 1; index <= 3; index++)
            session.SetPropertyValue(button, "Value", DesignPropertyValue.FromInt32(index));

        Assert.True(session.Transactions.Undo());
        Assert.True(session.Transactions.Undo());
        Assert.False(session.Transactions.Undo());
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void ReplayObserverMutationAppliesWithoutCreatingHistory()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Changed"));
        var appliedDerivedState = false;
        session.DocumentChanged += (_, _) =>
        {
            if (session.Transactions.ReplayMode == DesignerHistoryReplayMode.Undoing && !appliedDerivedState)
            {
                appliedDerivedState = true;
                session.SetPropertyValue(button, "Derived", DesignPropertyValue.FromBoolean(true));
            }
        };

        session.Transactions.Undo();

        Assert.True(button.Properties["Derived"].Value is true);
        Assert.False(session.Transactions.CanUndo);
        Assert.True(session.Transactions.CanRedo);
    }

    [Fact]
    public void ExceptionDisposalRollsBackEarlierChanges()
    {
        using var session = CreateSession(out var button);

        Assert.Throws<ExpectedDesignerException>(() => MutateAndThrow(session, button));

        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void CommitObserverExceptionLeavesCompletedHistoryUsable()
    {
        using var session = CreateSession(out var button);
        session.Transactions.TransactionCommitted += ThrowExpectedDesignerException;

        Assert.Throws<ExpectedDesignerException>(() =>
            session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Committed")));

        Assert.False(session.Transactions.HasActiveTransaction);
        Assert.Equal(DesignerHistoryReplayMode.Idle, session.Transactions.ReplayMode);
        Assert.True(session.Transactions.CanUndo);
        session.Transactions.TransactionCommitted -= ThrowExpectedDesignerException;
        Assert.True(session.Transactions.Undo());
        Assert.False(button.Properties.ContainsKey("Text"));
    }

    [Fact]
    public void RollbackObserverExceptionRestoresStateAndReplayMode()
    {
        using var session = CreateSession(out var button);
        var transaction = session.Transactions.Begin("Cancelled edit");
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Temporary"));
        session.Transactions.TransactionRolledBack += ThrowExpectedDesignerException;

        Assert.Throws<ExpectedDesignerException>(transaction.Rollback);

        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.False(session.Transactions.HasActiveTransaction);
        Assert.Equal(DesignerHistoryReplayMode.Idle, session.Transactions.ReplayMode);
        Assert.False(session.Transactions.CanUndo);
        transaction.Dispose();
    }

    [Fact]
    public void DeleteUndoRestoresDeletedSelectionAndRedoRestoresParentSelection()
    {
        var document = CreateDocument();
        var panel = document.Controls[0];
        var button = panel.Children.AddNode("Button", "nested", new DesignBounds(1, 1, 60, 24));
        using var session = CreateSession(document);
        session.SelectNode(button);

        session.DeleteNode(button);
        Assert.Same(panel, session.SelectedNode);
        session.Transactions.Undo();
        Assert.Same(button, session.SelectedNode);
        session.Transactions.Redo();
        Assert.Same(panel, session.SelectedNode);
    }

    [Theory]
    [InlineData(DesignRootKind.Form)]
    [InlineData(DesignRootKind.UserControl)]
    public void FormAndUserControlRootsShareTheSameHistoryBehavior(DesignRootKind rootKind)
    {
        var document = CreateDocument();
        document.RootKind = rootKind;
        using var session = CreateSession(document);

        session.ResizeDesignRoot(new DesignSize(900, 700));
        session.Transactions.Undo();

        Assert.Equal(new DesignSize(800, 600), document.Size);
    }

    [Fact]
    public void RootResizeUndoRestoresAnchorDerivedDescendantBounds()
    {
        using var session = CreateSession(out var button);
        button.Properties["Anchor"] = DesignPropertyValue.FromEnum(
            typeof(AnchorStyles).FullName!,
            (AnchorStyles.Right | AnchorStyles.Bottom).ToString());
        var originalBounds = button.Bounds;

        session.ResizeDesignRoot(new DesignSize(900, 700));
        Assert.NotEqual(originalBounds, button.Bounds);

        session.Transactions.Undo();
        Assert.Equal(new DesignSize(800, 600), session.Document.Size);
        Assert.Equal(originalBounds, button.Bounds);
    }

    [Fact]
    public void NestedProjectUserControlRemainsAtomicDuringPropertyReplay()
    {
        var document = CreateDocument();
        var custom = document.Controls.AddNode("Example.CustomPanel", "customPanel1", new DesignBounds(100, 100, 200, 120));
        custom.Children.AddNode("Button", "internalButton", new DesignBounds(5, 5, 80, 25));
        using var session = CreateSession(document);

        session.SetPropertyValue(custom, "Padding", DesignPropertyValue.FromInt32(12));
        session.Transactions.Undo();
        session.Transactions.Redo();

        Assert.Single(custom.Children);
        Assert.Equal(12, custom.Properties["Padding"].Value);
    }

    [Fact]
    public void SerializationAndCodeGenerationAreDeterministicAcrossUndoRedo()
    {
        using var session = CreateSession(out var button);
        var serializer = DesignDocumentSerializer.Default;
        var generator = new CSharpDesignerGenerator();
        var beforeJson = serializer.Serialize(session.Document);
        var beforeCode = generator.Generate(session.Document).Code;

        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Changed"));
        var afterJson = serializer.Serialize(session.Document);
        var afterCode = generator.Generate(session.Document).Code;
        session.Transactions.Undo();
        Assert.Equal(beforeJson, serializer.Serialize(session.Document));
        Assert.Equal(beforeCode, generator.Generate(session.Document).Code);
        session.Transactions.Redo();
        Assert.Equal(afterJson, serializer.Serialize(session.Document));
        Assert.Equal(afterCode, generator.Generate(session.Document).Code);
    }

    [Fact]
    public void ReverseParsedDocumentLoadsWithEmptyCleanHistory()
    {
        var source = new CSharpDesignerGenerator().Generate(CreateDocument()).Code;
        var parseResult = new CSharpDesignerParser().Parse(source);
        Assert.True(parseResult.Success);
        using var session = new DesignerSession();

        session.LoadDocument(Assert.IsType<DesignDocument>(parseResult.Document));

        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void ReverseImportReplacementIsOneUndoableDocumentChange()
    {
        var original = CreateDocument();
        var imported = CreateDocument();
        imported.FormName = "ImportedForm";
        imported.Controls.Clear();
        using var session = CreateSession(original);

        session.ReplaceDocument(imported, "Import designer code");
        Assert.Same(imported, session.Document);
        session.Transactions.Undo();
        Assert.Same(original, session.Document);
        session.Transactions.Redo();
        Assert.Same(imported, session.Document);
    }

    [Fact]
    public void ActiveTransactionRejectsUndoRedoAndDocumentSwitch()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Existing history"));
        using var transaction = session.Transactions.Begin("Active edit");

        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.CanRedo);
        Assert.Throws<InvalidOperationException>(() => session.Transactions.Undo());
        Assert.Throws<InvalidOperationException>(() => session.Transactions.Redo());
        Assert.Throws<InvalidOperationException>(() => session.LoadDocument(CreateDocument()));
    }

    [Fact]
    public void EmptyTransactionRestoresCommandAvailabilityNotification()
    {
        using var session = CreateSession(out var button);
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Existing history"));
        var notifications = 0;
        session.Transactions.HistoryChanged += (_, _) => notifications++;

        using (var transaction = session.Transactions.Begin("No-op edit"))
        {
            Assert.False(session.Transactions.CanUndo);
            transaction.Commit();
        }

        Assert.True(session.Transactions.CanUndo);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void OpenDocumentsKeepIndependentHistoryAndSavedRevisions()
    {
        var first = CreateDocument();
        var second = CreateDocument();
        second.ClassName = "SecondForm";
        using var session = CreateSession(first);
        session.SetPropertyValue(first.Controls[0], "Text", DesignPropertyValue.FromString("First"));

        session.OpenDocument(second, "SecondForm.mfdesign");
        session.SetPropertyValue(second.Controls[0], "Text", DesignPropertyValue.FromString("Second"));
        session.MarkSaved();
        Assert.False(session.IsDirty);

        session.SwitchDocument(0);
        Assert.True(session.IsDirty);
        Assert.Equal("Change Text", session.Transactions.UndoDescription);
        session.Transactions.Undo();
        Assert.False(first.Controls[0].Properties.ContainsKey("Text"));

        session.SwitchDocument(1);
        Assert.False(session.IsDirty);
        Assert.Equal("Change Text", session.Transactions.UndoDescription);
    }

    [Fact]
    public void InvalidPropertyGridValueLeavesModelAndHistoryUnchanged()
    {
        using var session = CreateSession(out var button);
        session.SelectNode(button);
        var state = new DesignerPropertyGridState(session);
        var width = FindProperty(state.Properties, "Width");
        state.SelectRow(new DesignerPropertyGridRow(width));

        Assert.False(state.CommitSelectedValue("0"));
        Assert.Equal(new DesignBounds(10, 10, 100, 30), button.Bounds);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public async Task TransactionManagerRejectsWrongThreadMutation()
    {
        using var session = CreateSession(out _);

        var exception = await Task.Run(() => Record.Exception(() => session.Transactions.Begin("Wrong thread")));

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void SessionDisposeReleasesSubtreeRetainedOnlyByDeleteHistory()
    {
        var retainedSubtree = CreateDisposedHistoryWeakReference();

        ForceFullCollection();

        Assert.False(retainedSubtree.IsAlive);
    }

    [Fact]
    public void SessionDisposeRollsBackActiveTransactionAndClearsHistory()
    {
        var session = CreateSession(out var button);
        var transaction = session.Transactions.Begin("Interrupted edit");
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Temporary"));

        session.Dispose();

        Assert.False(button.Properties.ContainsKey("Text"));
        Assert.False(session.Transactions.HasActiveTransaction);
        Assert.False(session.Transactions.CanUndo);
        transaction.Dispose();
    }

    [Fact]
    public void FormEditingScenarioUndoAllRedoAllRestoresExactTree()
    {
        using var session = CreateEmptySession();
        var serializer = DesignDocumentSerializer.Default;
        var initial = serializer.Serialize(session.Document);
        var panel = session.AddControl("Panel");
        session.SelectNode(panel);
        var textBox = session.AddControl("TextBox");
        session.SetPropertyValue(panel, "Padding", DesignPropertyValue.FromString("8, 8, 8, 8"));
        session.SetNodeBounds(textBox, new DesignBounds(20, 24, 180, 36));
        var final = serializer.Serialize(session.Document);

        for (var index = 0; index < 4; index++)
            Assert.True(session.Transactions.Undo());
        Assert.Equal(initial, serializer.Serialize(session.Document));

        for (var index = 0; index < 4; index++)
            Assert.True(session.Transactions.Redo());
        Assert.Equal(final, serializer.Serialize(session.Document));
    }

    [Fact]
    public void CustomUserControlScenarioUndoReturnsToSavedRevision()
    {
        var document = CreateDocument();
        document.RootKind = DesignRootKind.UserControl;
        var custom = document.Controls.AddNode("Example.AddressEditor", "addressEditor1", new DesignBounds(80, 80, 260, 140));
        var nested = custom.Children.AddNode("TextBox", "streetTextBox", new DesignBounds(10, 10, 180, 30));
        using var session = CreateSession(document);
        session.SetPropertyValue(nested, "Text", DesignPropertyValue.FromString("Saved street"));
        session.MarkSaved();
        session.SetPropertyValue(nested, "Margin", DesignPropertyValue.FromString("6, 6, 6, 6"));

        Assert.True(session.IsDirty);
        session.Transactions.Undo();
        Assert.False(session.IsDirty);
        Assert.Equal("Saved street", nested.Properties["Text"].GetString());
    }

    [Fact]
    public void PointsEditorScenarioCancelThenCommitIsOneUndoUnit()
    {
        using var session = CreateSession(out var button);
        using (var cancelled = session.Transactions.Begin("Edit Points"))
        {
            var snapshot = DesignerModelMutationSnapshot.CaptureNode(button);
            button.Properties["Points"] = DesignPropertyValue.FromString("0,0;10,10");
            snapshot.RecordChanges(session.Transactions);
            cancelled.Rollback();
        }

        Assert.False(session.Transactions.CanUndo);
        session.SetPropertyValue(button, "Points", DesignPropertyValue.FromString("0,0;20,20"));
        var committed = DesignDocumentSerializer.Default.Serialize(session.Document);
        session.Transactions.Undo();
        Assert.False(button.Properties.ContainsKey("Points"));
        session.Transactions.Redo();
        Assert.Equal(committed, DesignDocumentSerializer.Default.Serialize(session.Document));
    }

    [Fact]
    public void DeterministicStressUndoAllRedoAllRestoresExactSnapshots()
    {
        using var session = CreateSession(out var button, historyLimit: 1200);
        var serializer = DesignDocumentSerializer.Default;
        var initial = serializer.Serialize(session.Document);

        for (var index = 0; index < 1000; index++)
        {
            session.SetPropertyValue(button, "StressValue", DesignPropertyValue.FromInt32(index));
            if (index % 25 == 0)
                session.SetNodeBounds(button, new DesignBounds(10 + index / 25, 10, 100, 30));
        }

        var final = serializer.Serialize(session.Document);
        var undoCount = 0;
        while (session.Transactions.Undo())
            undoCount++;
        Assert.Equal(1039, undoCount);
        Assert.Equal(initial, serializer.Serialize(session.Document));

        var redoCount = 0;
        while (session.Transactions.Redo())
            redoCount++;
        Assert.Equal(1039, redoCount);
        Assert.Equal(final, serializer.Serialize(session.Document));
    }

    private static DesignerSession CreateEmptySession(int historyLimit = 500)
        => CreateSession(new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "mainForm",
            Size = new DesignSize(800, 600)
        }, historyLimit);

    private static DesignerSession CreateSession(out DesignControlNode button, int historyLimit = 500)
    {
        var document = CreateDocument();
        button = document.Controls[0];
        return CreateSession(document, historyLimit);
    }

    private static DesignerSession CreateSession(DesignDocument document, int historyLimit = 500)
    {
        var session = new DesignerSession(
            null,
            ModernFormsNext.Designer.Surface.DesignerControlRenderMode.Runtime,
            historyLimit);
        session.LoadDocument(document);
        return session;
    }

    private static DesignDocument CreateDocument()
    {
        var document = new DesignDocument
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "mainForm",
            Size = new DesignSize(800, 600)
        };
        document.Controls.AddNode("Button", "button1", new DesignBounds(10, 10, 100, 30));
        return document;
    }

    private static DesignerPropertyDescriptor FindProperty(
        IEnumerable<DesignerPropertyDescriptor> properties,
        string identity)
    {
        foreach (var property in properties)
        {
            if (string.Equals(property.Identity, identity, StringComparison.Ordinal))
                return property;

            var nested = FindPropertyOrDefault(property.Children, identity);
            if (nested is not null)
                return nested;
        }

        throw new InvalidOperationException($"Designer property '{identity}' was not found.");
    }

    private static DesignerPropertyDescriptor? FindPropertyOrDefault(
        IEnumerable<DesignerPropertyDescriptor> properties,
        string identity)
    {
        foreach (var property in properties)
        {
            if (string.Equals(property.Identity, identity, StringComparison.Ordinal))
                return property;
            var nested = FindPropertyOrDefault(property.Children, identity);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static void MutateAndThrow(DesignerSession session, DesignControlNode button)
    {
        using var transaction = session.Transactions.Begin("Failing transaction");
        session.SetPropertyValue(button, "Text", DesignPropertyValue.FromString("Temporary"));
        session.SetNodeBounds(button, new DesignBounds(80, 80, 200, 50));
        throw new ExpectedDesignerException();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedHistoryWeakReference()
    {
        var document = CreateDocument();
        var panel = document.Controls[0];
        panel.TypeName = "Panel";
        panel.Children.AddNode("Button", "nested", new DesignBounds(1, 1, 50, 20));
        var reference = new WeakReference(panel);
        var session = CreateSession(document);
        session.DeleteNode(panel);
        session.Dispose();
        return reference;
    }

    private static void ForceFullCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static void ThrowExpectedDesignerException(object? sender, DesignerHistoryEventArgs e)
        => throw new ExpectedDesignerException();

    private sealed class ExpectedDesignerException : Exception;
}

internal static class DesignerTransactionTestCollectionExtensions
{
    public static DesignControlNode AddNode(
        this DesignControlCollection collection,
        string typeName,
        string name,
        DesignBounds bounds)
    {
        var node = new DesignControlNode
        {
            TypeName = typeName,
            Name = name,
            Bounds = bounds
        };
        collection.Add(node);
        return node;
    }
}
