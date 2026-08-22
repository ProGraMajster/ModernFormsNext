using System.Diagnostics;
using System.Runtime.CompilerServices;
using ModernFormsNext;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Clipboard;
using ModernFormsNext.Designer.History;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerClipboardTests
{
    [Fact]
    public void CopySimpleButtonStoresVersionedDetachedPayloadWithoutHistoryOrDirtyState()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.MarkSaved();

        Assert.True(session.CopySelectedNode());

        Assert.Contains("\"format\":\"ModernFormsNext.Designer\"", session.Clipboard.Content, StringComparison.Ordinal);
        Assert.Contains("\"version\":1", session.Clipboard.Content, StringComparison.Ordinal);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
        Assert.Same(button, session.SelectedNode);
    }

    [Fact]
    public void PasteSimpleButtonCreatesOneUndoableUnitAndFreshNode()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());

        var pasted = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.NotSame(button, pasted);
        Assert.Equal("button2", pasted.Name);
        Assert.Equal(new DesignBounds(26, 26, 100, 30), pasted.Bounds);
        Assert.Equal("Paste button2", session.Transactions.UndoDescription);
        Assert.True(session.Transactions.Undo());
        Assert.Single(session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
        Assert.True(session.Transactions.Redo());
        Assert.Same(pasted, session.SelectedNode);
    }

    [Fact]
    public void DuplicateSimpleButtonCreatesOneUnitWithoutChangingClipboard()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.Clipboard.SetContent("{\"format\":\"unrelated\"}");
        var previousClipboard = session.Clipboard.Content;
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var duplicate = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal("button2", duplicate.Name);
        Assert.Equal(previousClipboard, session.Clipboard.Content);
        Assert.Equal("Duplicate button1", session.Transactions.UndoDescription);
        Assert.True(session.Transactions.Undo());
        Assert.Single(session.Document.Controls);
        Assert.True(session.Transactions.Redo());
        Assert.Same(duplicate, session.SelectedNode);
    }

    [Fact]
    public void CutSimpleButtonIsOneAtomicUndoRedoUnit()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);

        Assert.True(session.CutSelectedNode());

        Assert.Empty(session.Document.Controls);
        Assert.Null(session.SelectedNode);
        Assert.Equal("Cut button1", session.Transactions.UndoDescription);
        Assert.True(session.CanPasteCopiedNode);
        Assert.True(session.Transactions.Undo());
        Assert.Same(button, Assert.Single(session.Document.Controls));
        Assert.Same(button, session.SelectedNode);
        Assert.True(session.Transactions.Redo());
        Assert.Empty(session.Document.Controls);
        Assert.Null(session.SelectedNode);
    }

    [Fact]
    public void CopyAndPastePreserveDeepNestedSubtreeOrderPropertiesAndEvents()
    {
        var document = EmptyDocument();
        var panel = Node("Panel", "panel1", 10, 20, 260, 180);
        panel.Events["Click"] = "panel1_Click";
        var inner = Node("Panel", "panel2", 5, 6, 180, 120);
        var button = Node("Button", "button1", 7, 8, 90, 30);
        button.Properties["Text"] = DesignPropertyValue.FromString("Nested");
        inner.Children.Add(button);
        inner.Children.Add(Node("Label", "label1", 9, 44, 100, 24));
        panel.Children.Add(inner);
        document.Controls.Add(panel);
        using var session = CreateSession(document);
        session.SelectNode(panel);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal("panel3", copy.Name);
        Assert.Equal("panel1_Click", copy.Events["Click"]);
        var copiedInner = Assert.Single(copy.Children);
        Assert.Equal("panel4", copiedInner.Name);
        Assert.Equal(["button2", "label2"], copiedInner.Children.Select(child => child.Name));
        Assert.Equal("Nested", copiedInner.Children[0].Properties["Text"].GetString());
    }

    [Fact]
    public void NamingRemapsEveryNodeAndSkipsNamesAlreadyUsedByTarget()
    {
        var document = EmptyDocument();
        var panel = Node("Panel", "panel1", 10, 10, 200, 120);
        panel.Children.Add(Node("Button", "button1", 5, 5, 80, 25));
        document.Controls.Add(panel);
        document.Controls.Add(Node("Button", "button2", 250, 10, 80, 25));
        document.Controls.Add(Node("Panel", "panel2", 250, 50, 120, 90));
        using var session = CreateSession(document);
        session.SelectNode(panel);
        session.CopySelectedNode();
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal("panel3", copy.Name);
        Assert.Equal("button3", Assert.Single(copy.Children).Name);
    }

    [Fact]
    public void NamingFillsNumericGapsAndSuffixesCustomNamesDeterministically()
    {
        var document = EmptyDocument();
        var first = Node("Button", "button1", 10, 10, 80, 25);
        document.Controls.Add(first);
        document.Controls.Add(Node("Button", "button3", 100, 10, 80, 25));
        var custom = Node("Example.ProjectCard", "projectCard", 10, 50, 160, 80);
        document.Controls.Add(custom);
        using var session = CreateSession(document);
        session.SelectNode(first);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());
        Assert.Equal("button2", session.SelectedNode!.Name);
        session.SelectNode(custom);
        Assert.True(session.DuplicateSelectedNode());
        Assert.Equal("projectCard1", session.SelectedNode!.Name);
    }

    [Fact]
    public void DuplicateNamesInsideLegacySourceSubtreeAreRemappedUniquely()
    {
        var document = EmptyDocument();
        var panel = Node("Panel", "panel1", 10, 10, 200, 120);
        panel.Children.Add(Node("Button", "button1", 5, 5, 80, 25));
        panel.Children.Add(Node("Button", "button1", 5, 40, 80, 25));
        document.Controls.Add(panel);
        using var session = CreateSession(document);
        session.SelectNode(panel);

        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(["button2", "button3"], copy.Children.Select(node => node.Name));
        Assert.Equal(copy.Children.Count, copy.Children.Select(node => node.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void RepeatedPasteProducesDeterministicSequentialNamesAndSerialization()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());

        Assert.Equal(["button1", "button2", "button3"], session.Document.Controls.Select(node => node.Name));
        var serialized = DesignDocumentSerializer.Default.Serialize(session.Document);
        Assert.True(session.Transactions.Undo());
        Assert.True(session.Transactions.Undo());
        Assert.Equal(["button1"], session.Document.Controls.Select(node => node.Name));
        Assert.True(session.Transactions.Redo());
        Assert.True(session.Transactions.Redo());
        Assert.Equal(["button1", "button2", "button3"], session.Document.Controls.Select(node => node.Name));
        Assert.Equal(serialized, DesignDocumentSerializer.Default.Serialize(session.Document));
    }

    [Fact]
    public void PasteIntoSelectedContainerAddsToItsChildren()
    {
        var document = EmptyDocument();
        var button = Node("Button", "button1", 10, 10, 80, 25);
        var panel = Node("Panel", "panel1", 120, 30, 220, 140);
        document.Controls.Add(button);
        document.Controls.Add(panel);
        using var session = CreateSession(document);
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(panel);

        Assert.True(session.PasteCopiedNode());
        Assert.Same(session.SelectedNode, Assert.Single(panel.Children));
    }

    [Fact]
    public void PasteWithSelectedLeafUsesItsParent()
    {
        var document = EmptyDocument();
        var first = Node("Button", "button1", 10, 10, 80, 25);
        var selectedLeaf = Node("Label", "label1", 100, 10, 80, 25);
        document.Controls.Add(first);
        document.Controls.Add(selectedLeaf);
        using var session = CreateSession(document);
        session.SelectNode(first);
        session.CopySelectedNode();
        session.SelectNode(selectedLeaf);

        Assert.True(session.PasteCopiedNode());
        Assert.Equal(3, document.Controls.Count);
        Assert.Same(session.SelectedNode, document.Controls[2]);
    }

    [Fact]
    public void StaleSelectedContainerIsRejectedAsPasteTarget()
    {
        using var session = CreateSession(CreateDocument(out var source));
        session.SelectNode(source);
        Assert.True(session.CopySelectedNode());
        var staleContainer = Node("Panel", "stalePanel", 0, 0, 100, 100);
        session.SelectNode(staleContainer);
        var before = DesignDocumentSerializer.Default.Serialize(session.Document);

        Assert.False(session.PasteCopiedNode());

        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ParentPayloadCanPasteIntoSourceDescendantWithoutCreatingCycle()
    {
        var document = EmptyDocument();
        var parent = Node("Panel", "panel1", 10, 10, 220, 140);
        var descendant = Node("Panel", "panel2", 5, 5, 150, 90);
        parent.Children.Add(descendant);
        document.Controls.Add(parent);
        using var session = CreateSession(document);
        session.SelectNode(parent);
        session.CopySelectedNode();
        session.SelectNode(descendant);

        Assert.True(session.PasteCopiedNode());

        var copy = Assert.Single(descendant.Children);
        Assert.NotSame(parent, copy);
        Assert.NotSame(descendant, Assert.Single(copy.Children));
    }

    [Theory]
    [InlineData(DesignRootKind.Form)]
    [InlineData(DesignRootKind.UserControl)]
    public void DesignRootCannotBeCopiedCutOrDuplicated(DesignRootKind rootKind)
    {
        var document = EmptyDocument();
        document.RootKind = rootKind;
        using var session = CreateSession(document);
        session.SelectNode(null);

        Assert.False(session.CopySelectedNode());
        Assert.False(session.CutSelectedNode());
        Assert.False(session.DuplicateSelectedNode());
        Assert.Contains("design root", session.OutputLines[^1], StringComparison.OrdinalIgnoreCase);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void StructuralSplitPanelCannotBeCopiedIndependently()
    {
        var document = EmptyDocument();
        var split = Node("SplitContainer", "splitContainer1", 10, 10, 300, 180);
        document.Controls.Add(split);
        DesignerSpecialContainers.InitializeNewNode(split);
        using var session = CreateSession(document);
        session.SelectNode(Assert.IsType<DesignControlNode>(DesignerSpecialContainers.GetPanel1(split)));

        Assert.False(session.CopySelectedNode());
        Assert.False(session.CutSelectedNode());
        Assert.False(session.DuplicateSelectedNode());
    }

    [Fact]
    public void SplitContainerSubtreeRemapsOwnedPanelNamesAndDisplayMetadata()
    {
        var document = EmptyDocument();
        var split = Node("SplitContainer", "splitContainer1", 10, 10, 300, 180);
        document.Controls.Add(split);
        DesignerSpecialContainers.InitializeNewNode(split);
        using var session = CreateSession(document);
        session.SelectNode(split);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal("splitContainer2", copy.Name);
        Assert.Equal(["splitContainer2Panel1", "splitContainer2Panel2"], copy.Children.Select(node => node.Name));
        Assert.Equal("splitContainer2.Panel1", copy.Children[0].Properties[DesignNodeRoleNames.DisplayNamePropertyName].GetString());
        Assert.Equal("splitContainer2.Panel2", copy.Children[1].Properties[DesignNodeRoleNames.DisplayNamePropertyName].GetString());
    }

    [Fact]
    public void TabPageCannotPasteIntoOrdinaryPanel()
    {
        var document = EmptyDocument();
        var tabControl = Node("TabControl", "tabControl1", 10, 10, 300, 180);
        DesignerSpecialContainers.InitializeNewNode(tabControl);
        var panel = Node("Panel", "panel1", 330, 10, 200, 120);
        document.Controls.Add(tabControl);
        document.Controls.Add(panel);
        using var session = CreateSession(document);
        session.SelectNode(tabControl.Children[0]);
        session.CopySelectedNode();
        session.SelectNode(panel);

        Assert.False(session.PasteCopiedNode());
        Assert.Empty(panel.Children);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void TabPagePastesDirectlyIntoSelectedTabControl()
    {
        var document = EmptyDocument();
        var tabControl = Node("TabControl", "tabControl1", 10, 10, 300, 180);
        DesignerSpecialContainers.InitializeNewNode(tabControl);
        document.Controls.Add(tabControl);
        using var session = CreateSession(document);
        var sourcePage = tabControl.Children[0];
        var initialPageCount = tabControl.Children.Count;
        session.SelectNode(sourcePage);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(tabControl);

        Assert.True(session.PasteCopiedNode());

        var pastedPage = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(initialPageCount + 1, tabControl.Children.Count);
        Assert.Same(pastedPage, tabControl.Children[^1]);
        Assert.True(DesignerSpecialContainers.IsTabPage(pastedPage));
    }

    [Theory]
    [InlineData("None", 26, 26)]
    [InlineData("Fill", 10, 10)]
    [InlineData("Top", 10, 10)]
    public void PositioningOffsetsOnlyAbsoluteNonDockedControls(string dock, int expectedX, int expectedY)
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Properties["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, dock);
        button.Properties["Anchor"] = DesignPropertyValue.FromEnum(typeof(AnchorStyles).FullName!, "Top, Left, Right");
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(new DesignBounds(expectedX, expectedY, 100, 30), copy.Bounds);
        Assert.Equal(dock, copy.Properties["Dock"].GetString());
        Assert.Equal("Top, Left, Right", copy.Properties["Anchor"].GetString());
    }

    [Fact]
    public void PaddingMarginAndSizeConstraintsArePreserved()
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Properties["Padding"] = Structured("ModernFormsNext.Padding", ("Left", 1), ("Top", 2), ("Right", 3), ("Bottom", 4));
        button.Properties["Margin"] = Structured("ModernFormsNext.Padding", ("Left", 4), ("Top", 3), ("Right", 2), ("Bottom", 1));
        button.Properties["MinimumSize"] = Structured("System.Drawing.Size", ("Width", 40), ("Height", 20));
        button.Properties["MaximumSize"] = Structured("System.Drawing.Size", ("Width", 200), ("Height", 100));
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        foreach (var propertyName in new[] { "Padding", "Margin", "MinimumSize", "MaximumSize" })
        {
            Assert.True(DesignerPropertyValueComparer.Equals(button.Properties[propertyName], copy.Properties[propertyName]));
            Assert.NotSame(button.Properties[propertyName], copy.Properties[propertyName]);
            Assert.NotSame(button.Properties[propertyName].ObjectProperties, copy.Properties[propertyName].ObjectProperties);
        }
    }

    [Fact]
    public void FlowLayoutPastePreservesSequenceWithoutApplyingCoordinateOffset()
    {
        var document = EmptyDocument();
        var source = Node("Button", "button1", 10, 20, 80, 25);
        var flow = Node("FlowLayoutPanel", "flowLayoutPanel1", 100, 30, 260, 140);
        document.Controls.Add(source);
        document.Controls.Add(flow);
        using var session = CreateSession(document);
        session.SelectNode(source);
        session.CopySelectedNode();
        session.SelectNode(flow);

        Assert.True(session.PasteCopiedNode());

        var copy = Assert.Single(flow.Children);
        Assert.Equal(source.Bounds, copy.Bounds);
    }

    [Fact]
    public void TableLayoutPasteAssignsNextAvailableCellAndResetsSpans()
    {
        var document = EmptyDocument();
        var source = Node("Button", "button1", 10, 20, 80, 25);
        var table = Node("TableLayoutPanel", "tableLayoutPanel1", 100, 30, 260, 140);
        DesignerSpecialContainers.InitializeNewNode(table);
        var existing = Node("Label", "label1", 0, 0, 80, 25);
        DesignerSpecialContainers.SetInt(existing, DesignerSpecialContainers.TableColumnPropertyName, 0);
        DesignerSpecialContainers.SetInt(existing, DesignerSpecialContainers.TableRowPropertyName, 0);
        table.Children.Add(existing);
        document.Controls.Add(source);
        document.Controls.Add(table);
        using var session = CreateSession(document);
        session.SelectNode(source);
        session.CopySelectedNode();
        session.SelectNode(table);

        Assert.True(session.PasteCopiedNode());

        var copy = table.Children[1];
        Assert.Equal(1, DesignerSpecialContainers.GetInt(copy, DesignerSpecialContainers.TableColumnPropertyName, -1));
        Assert.Equal(0, DesignerSpecialContainers.GetInt(copy, DesignerSpecialContainers.TableRowPropertyName, -1));
        Assert.Equal(1, DesignerSpecialContainers.GetInt(copy, DesignerSpecialContainers.TableColumnSpanPropertyName, -1));
        Assert.Equal(1, DesignerSpecialContainers.GetInt(copy, DesignerSpecialContainers.TableRowSpanPropertyName, -1));
    }

    [Fact]
    public void SupportedCollectionsAreDeepCopiedWithoutSharedDesignerValues()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var effect = DesignPropertyValue.FromStructuredObject(
            "ModernFormsNext.Animations.ScaleInteractionEffect",
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Scale"] = DesignPropertyValue.FromDouble(1.125),
                ["Duration"] = DesignPropertyValue.FromDouble(120.5)
            });
        button.Properties[InteractionEffectDesignValue.PropertyName] = InteractionEffectDesignValue.Create([effect]);
        button.Properties[LayoutTransitionDesignValue.PropertyName] = LayoutTransitionDesignValue.Create(true, 225.5, "EaseOut");
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        foreach (var name in new[] { InteractionEffectDesignValue.PropertyName, LayoutTransitionDesignValue.PropertyName })
        {
            Assert.True(DesignerPropertyValueComparer.Equals(button.Properties[name], copy.Properties[name]));
            Assert.NotSame(button.Properties[name].ObjectProperties, copy.Properties[name].ObjectProperties);
        }
    }

    [Fact]
    public void BrushGradientAndGeometryValuesRoundTripWithExactKindsAndIndependentGraphs()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var brush = new LinearGradientBrush();
        brush.GradientStops.Add(new GradientStop(new SKColor(10, 20, 30), 0f));
        brush.GradientStops.Add(new GradientStop(new SKColor(40, 50, 60), 1f));
        var geometry = new PathGeometry();
        var figure = new PathFigure(new System.Drawing.PointF(1.5f, 2.5f), isClosed: true);
        figure.Segments.Add(new LineSegment(new System.Drawing.PointF(30.5f, 40.5f)));
        geometry.Figures.Add(figure);
        button.Properties["Background"] = DesignerPropertyValueEditor.ToDesignPropertyValue(brush, typeof(Brush));
        button.Properties["Data"] = DesignerPropertyValueEditor.ToDesignPropertyValue(geometry, typeof(Geometry));
        button.Properties["IntegralDouble"] = DesignPropertyValue.FromDouble(2d);
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        foreach (var name in new[] { "Background", "Data", "IntegralDouble" })
        {
            Assert.True(DesignerPropertyValueComparer.Equals(button.Properties[name], copy.Properties[name]));
            Assert.NotSame(button.Properties[name], copy.Properties[name]);
        }
        Assert.Equal(DesignPropertyValueKind.Double, copy.Properties["IntegralDouble"].Kind);
    }

    [Fact]
    public void TabControlCopyPreservesTabPagesAndTheirChildrenAsIndependentNodes()
    {
        var document = EmptyDocument();
        var tabs = Node("TabControl", "tabControl1", 10, 10, 300, 180);
        DesignerSpecialContainers.InitializeNewNode(tabs);
        tabs.Children[0].Children.Add(Node("Button", "button1", 5, 5, 80, 25));
        document.Controls.Add(tabs);
        using var session = CreateSession(document);
        session.SelectNode(tabs);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(2, copy.Children.Count);
        Assert.NotSame(tabs.Children[0], copy.Children[0]);
        Assert.NotSame(tabs.Children[0].Children[0], copy.Children[0].Children[0]);
        Assert.Equal("button2", copy.Children[0].Children[0].Name);
    }

    [Theory]
    [InlineData("UserControl")]
    [InlineData("Example.ProjectCard")]
    public void UserControlAndUnavailableCustomTypesPasteAsSafeDataOnlyNodes(string typeName)
    {
        ConstructorProbe.ConstructionCount = 0;
        var document = EmptyDocument();
        var source = Node(typeName, "projectCard1", 10, 10, 160, 80);
        source.Properties["Text"] = DesignPropertyValue.FromString("Card");
        document.Controls.Add(source);
        using var session = CreateSession(document);
        session.SelectNode(source);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(typeName, copy.TypeName);
        Assert.Equal("Card", copy.Properties["Text"].GetString());
        Assert.Equal(0, ConstructorProbe.ConstructionCount);
    }

    [Fact]
    public void ClipboardDoesNotConstructEvenAnAvailableCustomControlType()
    {
        ConstructorProbe.ConstructionCount = 0;
        var document = EmptyDocument();
        var source = Node(typeof(ConstructorProbe).FullName!, "constructorProbe1", 10, 10, 160, 80);
        document.Controls.Add(source);
        using var session = CreateSession(document);
        session.SelectNode(source);

        Assert.True(session.DuplicateSelectedNode());

        Assert.Equal(typeof(ConstructorProbe).FullName, session.SelectedNode!.TypeName);
        Assert.Equal(0, ConstructorProbe.ConstructionCount);
    }

    [Fact]
    public void CopyPreservesEventHandlerNamesWithoutCreatingDelegatesOrMethods()
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Events["Click"] = "HandleButtonClick";
        button.Events["TextChanged"] = null;
        session.SelectNode(button);

        Assert.True(session.DuplicateSelectedNode());

        var copy = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal("HandleButtonClick", copy.Events["Click"]);
        Assert.Null(copy.Events["TextChanged"]);
        var code = new CSharpDesignerGenerator().Generate(session.Document).Code;
        Assert.Contains("button2.Click += this.HandleButtonClick;", code, StringComparison.Ordinal);
        Assert.DoesNotContain("void HandleButtonClick", code, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossDocumentCopyCreatesIndependentTransactionInTargetDocument()
    {
        var first = CreateDocument(out var source);
        first.ClassName = "FirstForm";
        var second = EmptyDocument();
        second.ClassName = "SecondForm";
        using var session = CreateSession(first, "First.mfdesign");
        session.SelectNode(source);
        session.CopySelectedNode();
        session.OpenDocument(second, "Second.mfdesign");
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());
        var pasted = Assert.Single(second.Controls);
        Assert.NotSame(source, pasted);
        Assert.True(session.Transactions.Undo());
        Assert.Empty(second.Controls);
        Assert.Single(first.Controls);
        session.SwitchDocument(0);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void ClipboardSurvivesSourceDocumentCloseBecauseItContainsNoLiveNodes()
    {
        var first = CreateDocument(out var source);
        first.ClassName = "FirstForm";
        var second = EmptyDocument();
        second.ClassName = "SecondForm";
        using var session = CreateSession(first, "First.mfdesign");
        session.SelectNode(source);
        session.CopySelectedNode();
        session.OpenDocument(second, "Second.mfdesign");
        session.CloseDocument(0);
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());
        Assert.Single(second.Controls);
    }

    [Fact]
    public void CutInFirstDocumentAndPasteInSecondKeepIndependentHistories()
    {
        var first = CreateDocument(out var source);
        first.ClassName = "FirstForm";
        var second = EmptyDocument();
        second.ClassName = "SecondForm";
        using var session = CreateSession(first, "First.mfdesign");
        session.SelectNode(source);
        Assert.True(session.CutSelectedNode());
        Assert.Empty(first.Controls);
        session.OpenDocument(second, "Second.mfdesign");
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());

        Assert.True(session.Transactions.Undo());
        Assert.Empty(second.Controls);
        Assert.Empty(first.Controls);
        session.SwitchDocument(0);
        Assert.True(session.Transactions.Undo());
        Assert.Same(source, Assert.Single(first.Controls));
        Assert.Empty(second.Controls);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("null")]
    public void CorruptedClipboardPayloadIsRejectedWithoutMutationOrHistory(string content)
    {
        using var session = CreateSession(CreateDocument(out _));
        if (string.IsNullOrEmpty(content))
            session.Clipboard.Clear();
        else
            session.Clipboard.SetContent(content);
        var before = DesignDocumentSerializer.Default.Serialize(session.Document);

        Assert.False(session.PasteCopiedNode());

        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.False(session.Transactions.CanUndo);
    }

    [Theory]
    [InlineData("\"version\":1", "\"version\":99")]
    [InlineData("ModernFormsNext.Designer", "ModernFormsNext.Unknown")]
    public void UnsupportedClipboardVersionOrFormatIsRejected(string oldText, string newText)
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.CopySelectedNode();
        session.Clipboard.SetContent(session.Clipboard.Content!.Replace(oldText, newText, StringComparison.Ordinal));
        var before = DesignDocumentSerializer.Default.Serialize(session.Document);

        Assert.False(session.PasteCopiedNode());
        Assert.False(session.CanPasteCopiedNode);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MissingSchemaFieldsInvalidCollectionsAndUnsafeNamesAreRejected()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        var valid = session.Clipboard.Content!;
        var before = DesignDocumentSerializer.Default.Serialize(session.Document);
        var invalidPayloads = new[]
        {
            valid.Replace($"\"format\":\"{DesignerClipboardPayload.CurrentFormat}\",", string.Empty, StringComparison.Ordinal),
            $"{{\"format\":\"{DesignerClipboardPayload.CurrentFormat}\",\"version\":1}}",
            valid.Replace("\"children\":[]", "\"children\":{}", StringComparison.Ordinal),
            valid.Replace("\"name\":\"button1\"", "\"name\":\"1button\"", StringComparison.Ordinal)
        };

        foreach (var payload in invalidPayloads)
        {
            session.Clipboard.SetContent(payload);
            Assert.False(session.PasteCopiedNode());
            Assert.False(session.CanPasteCopiedNode);
            Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(session.Document));
            Assert.False(session.Transactions.CanUndo);
        }
    }

    [Fact]
    public void AssemblyQualifiedTypeNamesAreRejectedBeforeDocumentMutation()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        session.Clipboard.SetContent(session.Clipboard.Content!.Replace(
            "\"typeName\":\"Button\"",
            "\"typeName\":\"Example.ProjectCard, Example.Project\"",
            StringComparison.Ordinal));

        Assert.False(session.PasteCopiedNode());
        Assert.Single(session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MalformedGeometryRepresentationIsRejectedBeforeDocumentMutation()
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Properties["Data"] = Structured("ModernFormsNext.Drawing.PathGeometry", ("FigureCount", 1));
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        session.Clipboard.SetContent(session.Clipboard.Content!.Replace(
            "\"properties\":{\"FigureCount\":{\"kind\":\"int32\",\"int32Value\":1}}",
            "\"properties\":null",
            StringComparison.Ordinal));

        Assert.False(session.PasteCopiedNode());
        Assert.Single(session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void PathologicallyDeepPayloadIsRejectedWithoutStackOverflowOrMutation()
    {
        using var session = CreateSession(CreateDocument(out _));
        session.Clipboard.SetContent(CreateDeepClipboardPayload(depth: 2_000));

        Assert.False(session.PasteCopiedNode());
        Assert.Single(session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MissingControlTypeIsRejectedWithoutHistory()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.CopySelectedNode();
        session.Clipboard.SetContent(session.Clipboard.Content!.Replace("\"typeName\":\"Button\"", "\"typeName\":null", StringComparison.Ordinal));

        Assert.False(session.PasteCopiedNode());
        Assert.False(session.Transactions.CanUndo);
        Assert.Single(session.Document.Controls);
    }

    [Fact]
    public void InvalidPropertyRepresentationAndUnknownMembersAreRejected()
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Properties["Count"] = DesignPropertyValue.FromInt32(7);
        session.SelectNode(button);
        session.CopySelectedNode();
        var valid = session.Clipboard.Content!;
        var invalidValue = valid.Replace("\"kind\":\"int32\",\"int32Value\":7", "\"kind\":\"boolean\"", StringComparison.Ordinal);
        session.Clipboard.SetContent(invalidValue);
        Assert.False(session.PasteCopiedNode());

        var contradictoryValue = valid.Replace(
            "\"kind\":\"int32\",\"int32Value\":7",
            "\"kind\":\"int32\",\"booleanValue\":true,\"int32Value\":7",
            StringComparison.Ordinal);
        session.Clipboard.SetContent(contradictoryValue);
        Assert.False(session.PasteCopiedNode());

        session.Clipboard.SetContent(valid.Replace("\"version\":1,", string.Empty, StringComparison.Ordinal));
        Assert.False(session.PasteCopiedNode());

        session.Clipboard.SetContent(valid.Replace("\"memberVisibility\":\"private\",", string.Empty, StringComparison.Ordinal));
        Assert.False(session.PasteCopiedNode());

        session.Clipboard.SetContent($"{valid[..^1]},\"unexpected\":true}}");
        Assert.False(session.PasteCopiedNode());
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void MaliciousEventOrTypeTextIsRejectedAndNeverExecuted()
    {
        using var session = CreateSession(CreateDocument(out var button));
        button.Events["Click"] = "HandleClick";
        session.SelectNode(button);
        session.CopySelectedNode();
        var valid = session.Clipboard.Content!;
        var malicious = valid
            .Replace("\"HandleClick\"", "\"HandleClick();System.IO.File.Delete('x')\"", StringComparison.Ordinal);
        session.Clipboard.SetContent(malicious);

        Assert.False(session.PasteCopiedNode());

        session.Clipboard.SetContent(valid.Replace(
            "\"typeName\":\"Button\"",
            "\"typeName\":\"Button;System.IO.File.Delete\"",
            StringComparison.Ordinal));
        Assert.False(session.PasteCopiedNode());

        session.Clipboard.SetContent(valid.Replace(
            "\"stringValue\":\"None\"",
            "\"stringValue\":\"None);System.IO.File.Delete('x')\"",
            StringComparison.Ordinal));
        Assert.False(session.PasteCopiedNode());

        Assert.Single(session.Document.Controls);
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void CommandAvailabilityTracksSelectionClipboardAndInvalidPayload()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        Assert.True(session.CanCopySelectedNode);
        Assert.True(session.CanCutSelectedNode);
        Assert.True(session.CanDuplicateSelectedNode);
        Assert.False(session.CanPasteCopiedNode);

        session.CopySelectedNode();
        Assert.True(session.CanPasteCopiedNode);
        session.SelectNode(null);
        Assert.False(session.CanCopySelectedNode);
        Assert.False(session.CanCutSelectedNode);
        Assert.False(session.CanDuplicateSelectedNode);
        Assert.True(session.CanPasteCopiedNode);

        session.Clipboard.SetContent("{");
        Assert.False(session.CanPasteCopiedNode);
        session.Clipboard.Clear();
        Assert.False(session.CanPasteCopiedNode);
    }

    [Fact]
    public void ToolbarCommandStatesRefreshAfterSelectionClipboardUndoAndDocumentSwitch()
    {
        using var shell = new ModernFormsDesignerShell();
        var first = CreateDocument(out var button);
        shell.Session.OpenDocument(first, path: null);
        var toolbar = Assert.Single(shell.Controls.OfType<DesignerToolbar>());
        var cut = GetToolbarButton(toolbar, "cutButton");
        var copy = GetToolbarButton(toolbar, "copyButton");
        var paste = GetToolbarButton(toolbar, "pasteButton");
        var duplicate = GetToolbarButton(toolbar, "duplicateButton");

        Assert.False(cut.Enabled);
        Assert.False(copy.Enabled);
        Assert.False(paste.Enabled);
        Assert.False(duplicate.Enabled);

        shell.Session.SelectNode(button);
        Assert.True(cut.Enabled);
        Assert.True(copy.Enabled);
        Assert.True(duplicate.Enabled);
        shell.Session.CopySelectedNode();
        Assert.True(paste.Enabled);

        Assert.True(shell.Session.CutSelectedNode());
        Assert.False(cut.Enabled);
        Assert.False(copy.Enabled);
        Assert.True(paste.Enabled);
        Assert.True(shell.Session.Transactions.Undo());
        Assert.True(copy.Enabled);

        shell.Session.OpenDocument(EmptyDocument(), path: null);
        Assert.False(copy.Enabled);
        Assert.True(paste.Enabled);
        shell.Session.CloseDocument(shell.Session.ActiveDocumentIndex);
        Assert.False(copy.Enabled);
        Assert.True(paste.Enabled);
    }

    [Fact]
    public void ClipboardChangedFiresOnlyWhenDetachedContentChanges()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var changed = 0;
        session.ClipboardChanged += (_, _) => changed++;
        session.SelectNode(button);

        session.CopySelectedNode();
        session.CopySelectedNode();
        session.Clipboard.Clear();

        Assert.Equal(2, changed);
    }

    [Theory]
    [InlineData(Keys.C, 1)]
    [InlineData(Keys.X, 0)]
    [InlineData(Keys.V, 2)]
    [InlineData(Keys.D, 2)]
    public void ShellHandlesDesignerClipboardShortcuts(Keys key, int expectedControlCount)
    {
        using var shell = new ModernFormsDesignerShell();
        var document = CreateDocument(out var button);
        shell.LoadDocument(document);
        shell.Session.SelectNode(button);
        if (key == Keys.V)
        {
            shell.Session.CopySelectedNode();
            shell.Session.SelectNode(null);
        }
        var args = new KeyEventArgs(Keys.Control | key);

        Assert.True(shell.ProcessDesignerShortcut(args));

        Assert.True(args.SuppressKeyPress);
        Assert.Equal(expectedControlCount, document.Controls.Count);
    }

    [Theory]
    [InlineData(Keys.C)]
    [InlineData(Keys.X)]
    [InlineData(Keys.V)]
    [InlineData(Keys.D)]
    public void ShellLeavesClipboardShortcutsToActivePropertyTextEditor(Keys key)
    {
        using var shell = new ModernFormsDesignerShell();
        var document = CreateDocument(out var button);
        shell.LoadDocument(document);
        shell.Session.SelectNode(button);
        Assert.True(shell.Session.CopySelectedNode());
        var grid = Assert.Single(shell.Controls.OfType<DesignerPropertyGrid>());
        var stateField = typeof(DesignerPropertyGrid).GetField(
            "state",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(stateField);
        var state = Assert.IsType<DesignerPropertyGridState>(stateField.GetValue(grid));
        var textRow = Assert.Single(state.Rows, row => row.Property?.Name == "Text");
        grid.BeginEdit(textRow, new System.Drawing.Rectangle(0, 0, 160, 24));
        Assert.True(grid.IsEditingValue);
        var before = DesignDocumentSerializer.Default.Serialize(document);
        var args = new KeyEventArgs(Keys.Control | key);

        Assert.False(shell.ProcessDesignerShortcut(args));

        Assert.False(args.SuppressKeyPress);
        Assert.Equal(before, DesignDocumentSerializer.Default.Serialize(document));
        Assert.False(shell.Session.Transactions.CanUndo);
    }

    [Fact]
    public void DirtyStateReturnsToSaveMarkerAfterUndoingPaste()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.MarkSaved();
        session.SelectNode(button);
        session.CopySelectedNode();
        Assert.False(session.IsDirty);
        session.SelectNode(null);

        session.PasteCopiedNode();
        Assert.True(session.IsDirty);
        session.Transactions.Undo();
        Assert.False(session.IsDirty);
        session.Transactions.Redo();
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void PasteUndoRedoRestoresSelectionForPropertyGridAndExistingControl()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var grid = new DesignerPropertyGridState(session);
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);
        session.PasteCopiedNode();
        var pasted = Assert.IsType<DesignControlNode>(session.SelectedNode);
        Assert.Equal(pasted.Name, grid.HeaderName);

        session.Transactions.Undo();
        Assert.Null(session.SelectedNode);
        Assert.Equal(session.Document.FormName, grid.HeaderName);
        session.Transactions.Redo();
        Assert.Same(pasted, session.SelectedNode);
        Assert.Equal(pasted.Name, grid.HeaderName);
    }

    [Fact]
    public void PastePublishesDocumentChangeUsedByOutlineAndPropertyViews()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var changes = 0;
        session.DocumentChanged += (_, _) => changes++;
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);

        session.PasteCopiedNode();
        session.Transactions.Undo();
        session.Transactions.Redo();

        Assert.Equal(3, changes);
        Assert.Equal(2, session.Document.Controls.Count);
    }

    [Fact]
    public void GeneratedCodeTracksPasteUndoAndRedoDeterministically()
    {
        using var session = CreateSession(CreateDocument(out var button));
        var generator = new CSharpDesignerGenerator();
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);
        session.PasteCopiedNode();
        var pastedCode = generator.Generate(session.Document);
        Assert.True(pastedCode.Succeeded, string.Join(Environment.NewLine, pastedCode.Validation.Errors));
        Assert.Contains("button2", pastedCode.Code, StringComparison.Ordinal);

        session.Transactions.Undo();
        Assert.DoesNotContain("button2", generator.Generate(session.Document).Code, StringComparison.Ordinal);
        session.Transactions.Redo();
        Assert.Equal(pastedCode.Code, generator.Generate(session.Document).Code);
    }

    [Fact]
    public void GeneratedCodePreservesPastedSubtreeHierarchy()
    {
        var document = EmptyDocument();
        var panel = Node("Panel", "panel1", 10, 10, 200, 120);
        panel.Children.Add(Node("Button", "button1", 5, 5, 80, 25));
        document.Controls.Add(panel);
        using var session = CreateSession(document);
        session.SelectNode(panel);
        session.DuplicateSelectedNode();

        var result = new CSharpDesignerGenerator().Generate(document);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Validation.Errors));
        Assert.Contains("this.panel2.Controls.Add(this.button2);", result.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveReopenAfterPastePreservesTreeAndDoesNotPersistClipboardPayload()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);
        session.PasteCopiedNode();

        var json = DesignDocumentSerializer.Default.Serialize(session.Document);
        var reopened = DesignDocumentSerializer.Default.Deserialize(json);

        Assert.Equal(["button1", "button2"], reopened.Controls.Select(node => node.Name));
        Assert.DoesNotContain(DesignerClipboardPayload.CurrentFormat, json, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseParsedDocumentSupportsCopyPasteLikeMfdesignDocument()
    {
        var original = CreateDocument(out _);
        var generated = new CSharpDesignerGenerator().Generate(original);
        Assert.True(generated.Succeeded);
        var parsed = new CSharpDesignerParser().Parse(generated.Code);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics.Select(item => item.Message)));
        using var session = CreateSession(Assert.IsType<DesignDocument>(parsed.Document));
        var source = Assert.Single(session.Document.Controls);
        session.SelectNode(source);
        session.CopySelectedNode();
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());
        Assert.Equal(2, session.Document.Controls.Count);
    }

    [Fact]
    public void NewPasteAfterUndoClearsRedoWithoutAffectingClipboard()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        session.CopySelectedNode();
        session.SelectNode(null);
        session.PasteCopiedNode();
        session.Transactions.Undo();
        Assert.True(session.Transactions.CanRedo);

        session.PasteCopiedNode();

        Assert.False(session.Transactions.CanRedo);
        Assert.Equal(2, session.Document.Controls.Count);
    }

    [Fact]
    public void CopyDoesNotClearRedoOrChangeDirtyState()
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.MarkSaved();
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());
        Assert.True(session.Transactions.Undo());
        Assert.True(session.Transactions.CanRedo);
        Assert.False(session.IsDirty);
        session.SelectNode(button);

        Assert.True(session.CopySelectedNode());

        Assert.True(session.Transactions.CanRedo);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void CutClipboardFailureBeforeMutationLeavesDocumentAndHistoryUntouched()
    {
        var clipboard = new ThrowAfterSetDesignerClipboard();
        using var session = new DesignerSession(
            environment: null,
            initialRenderMode: DesignerControlRenderMode.Runtime,
            historyLimit: 500,
            clipboard: clipboard);
        var document = CreateDocument(out var button);
        session.OpenDocument(document, path: null);
        session.SelectNode(button);

        Assert.Throws<InvalidOperationException>(() => session.CutSelectedNode());

        Assert.Same(button, Assert.Single(document.Controls));
        Assert.Same(button, session.SelectedNode);
        Assert.NotNull(clipboard.Content);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.HasActiveTransaction);
    }

    [Theory]
    [InlineData("Cut")]
    [InlineData("Paste")]
    [InlineData("Duplicate")]
    public void ClipboardMutationRollsBackWhenSelectionRefreshFailsAfterTreeChange(string operation)
    {
        using var session = CreateSession(CreateDocument(out var button));
        session.SelectNode(button);
        Assert.True(session.CopySelectedNode());
        var clipboardBefore = session.Clipboard.Content;
        if (operation == "Paste")
            session.SelectNode(null);
        var selectionBefore = session.SelectedNode;
        var treeBefore = DesignDocumentSerializer.Default.Serialize(session.Document);
        EventHandler? throwOnce = null;
        throwOnce = (_, _) =>
        {
            session.Host.Selection.SelectionChanged -= throwOnce;
            throw new InvalidOperationException("Expected selection refresh failure.");
        };
        session.Host.Selection.SelectionChanged += throwOnce;

        Assert.Throws<InvalidOperationException>(() =>
        {
            if (operation == "Cut")
                session.CutSelectedNode();
            else if (operation == "Paste")
                session.PasteCopiedNode();
            else
                session.DuplicateSelectedNode();
        });

        Assert.Equal(treeBefore, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.Same(selectionBefore, session.SelectedNode);
        Assert.False(session.Transactions.CanUndo);
        Assert.False(session.Transactions.HasActiveTransaction);
        Assert.Equal(clipboardBefore, session.Clipboard.Content);
    }

    [Fact]
    public void LastCopyWinsAcrossThreeDocuments()
    {
        var first = CreateDocument(out var firstButton);
        firstButton.Properties["Text"] = DesignPropertyValue.FromString("First");
        var second = CreateDocument(out var secondButton);
        second.ClassName = "SecondForm";
        secondButton.Properties["Text"] = DesignPropertyValue.FromString("Second");
        var third = EmptyDocument();
        third.ClassName = "ThirdForm";
        using var session = CreateSession(first, "First.mfdesign");
        session.SelectNode(firstButton);
        Assert.True(session.CopySelectedNode());
        session.OpenDocument(second, "Second.mfdesign");
        session.SelectNode(secondButton);
        Assert.True(session.CopySelectedNode());
        session.OpenDocument(third, "Third.mfdesign");
        session.SelectNode(null);

        Assert.True(session.PasteCopiedNode());

        Assert.Equal("Second", Assert.Single(third.Controls).Properties["Text"].GetString());
    }

    [Fact]
    public void SourceDocumentCanCloseAndCollectBeforePasteAndUndoInTarget()
    {
        var (session, target, sourceReference) = CreateClosedSourceClipboardScenario();
        using (session)
        {
            ForceFullCollection();
            Assert.False(sourceReference.IsAlive);
            Assert.True(session.PasteCopiedNode());
            Assert.Single(target.Controls);
            Assert.True(session.Transactions.Undo());
            Assert.Empty(target.Controls);
        }
    }

    [Fact]
    public void ClipboardPayloadDoesNotRetainDeletedOriginalSubtreeAfterHistoryClear()
    {
        var (session, reference) = CreateClipboardWeakReference();
        using (session)
        {
            ForceFullCollection();
            Assert.False(reference.IsAlive);
            Assert.True(session.CanPasteCopiedNode);
            Assert.True(session.PasteCopiedNode());
        }
    }

    [Fact]
    public void LargeSubtreeCopyPasteUndoRedoIsDeterministicAndBounded()
    {
        var document = EmptyDocument();
        var root = Node("Panel", "panel1", 10, 10, 600, 500);
        for (var index = 1; index <= 300; index++)
        {
            var container = Node("Panel", $"cell{index}", index % 20 * 20, index / 20 * 20, 18, 18);
            if (index % 10 == 0)
                container.Children.Add(Node("Label", $"label{index}", 1, 1, 12, 12));
            root.Children.Add(container);
        }
        document.Controls.Add(root);
        using var session = CreateSession(document);
        session.SelectNode(root);
        var stopwatch = Stopwatch.StartNew();
        Assert.True(session.CopySelectedNode());
        session.SelectNode(null);
        Assert.True(session.PasteCopiedNode());
        var pastedSnapshot = DesignDocumentSerializer.Default.Serialize(session.Document);
        Assert.True(session.Transactions.Undo());
        Assert.Single(session.Document.Controls);
        Assert.True(session.Transactions.Redo());
        stopwatch.Stop();

        Assert.Equal(pastedSnapshot, DesignDocumentSerializer.Default.Serialize(session.Document));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Large subtree clipboard test took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void ClipboardSerializationIsDeterministicForSameSubtree()
    {
        var document = CreateDocument(out var button);
        button.Properties["Zeta"] = DesignPropertyValue.FromInt32(1);
        button.Properties["Alpha"] = DesignPropertyValue.FromDouble(2d);

        Assert.True(DesignerClipboardSerializer.TrySerialize(button, out var first, out _));
        Assert.True(DesignerClipboardSerializer.TrySerialize(button, out var second, out _));
        Assert.Equal(first, second);
    }

    private static DesignerSession CreateSession(DesignDocument document, string? path = null)
    {
        var session = new DesignerSession();
        session.OpenDocument(document, path);
        return session;
    }

    private static string CreateDeepClipboardPayload(int depth)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append($"{{\"format\":\"{DesignerClipboardPayload.CurrentFormat}\",\"version\":1,\"root\":");
        for (var index = 0; index < depth; index++)
        {
            builder.Append($"{{\"typeName\":\"Panel\",\"name\":\"panel{index}\",\"x\":0,\"y\":0,\"width\":1,\"height\":1,\"memberVisibility\":\"private\",\"properties\":{{}},\"events\":{{}},\"children\":[");
        }

        builder.Append($"{{\"typeName\":\"Panel\",\"name\":\"panel{depth}\",\"x\":0,\"y\":0,\"width\":1,\"height\":1,\"memberVisibility\":\"private\",\"properties\":{{}},\"events\":{{}},\"children\":[]}}");
        for (var index = 0; index < depth; index++)
            builder.Append("]}");
        builder.Append('}');
        return builder.ToString();
    }

    private static DesignDocument CreateDocument(out DesignControlNode button)
    {
        var document = EmptyDocument();
        button = Node("Button", "button1", 10, 10, 100, 30);
        document.Controls.Add(button);
        return document;
    }

    private static DesignDocument EmptyDocument()
        => new()
        {
            Namespace = "Example",
            ClassName = "MainForm",
            FormName = "mainForm",
            Size = new DesignSize(800, 600)
        };

    private static DesignControlNode Node(string typeName, string name, int x, int y, int width, int height)
        => new()
        {
            TypeName = typeName,
            Name = name,
            Bounds = new DesignBounds(x, y, width, height),
            Properties =
            {
                ["Dock"] = DesignPropertyValue.FromEnum(typeof(DockStyle).FullName!, nameof(DockStyle.None))
            }
        };

    private static DesignPropertyValue Structured(string typeName, params (string Name, int Value)[] properties)
        => DesignPropertyValue.FromStructuredObject(
            typeName,
            properties.ToDictionary(item => item.Name, item => DesignPropertyValue.FromInt32(item.Value), StringComparer.Ordinal));

    private static Button GetToolbarButton(DesignerToolbar toolbar, string fieldName)
    {
        var field = typeof(DesignerToolbar).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Button>(field.GetValue(toolbar));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DesignerSession Session, WeakReference Reference) CreateClipboardWeakReference()
    {
        var document = EmptyDocument();
        var panel = Node("Panel", "panel1", 10, 10, 200, 120);
        panel.Children.Add(Node("Button", "button1", 5, 5, 80, 25));
        document.Controls.Add(panel);
        var reference = new WeakReference(panel);
        var session = CreateSession(document);
        session.SelectNode(panel);
        session.CopySelectedNode();
        session.DeleteNode(panel);
        session.Transactions.ClearHistory();
        return (session, reference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DesignerSession Session, DesignDocument Target, WeakReference Reference) CreateClosedSourceClipboardScenario()
    {
        var sourceDocument = CreateDocument(out var source);
        var reference = new WeakReference(source);
        var target = EmptyDocument();
        target.ClassName = "TargetForm";
        var session = CreateSession(sourceDocument, "Source.mfdesign");
        session.SelectNode(source);
        session.CopySelectedNode();
        session.OpenDocument(target, "Target.mfdesign");
        session.CloseDocument(0);
        session.SelectNode(null);
        return (session, target, reference);
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

    private sealed class ConstructorProbe : UserControl
    {
        public static int ConstructionCount { get; set; }

        public ConstructorProbe() => ConstructionCount++;
    }

    private sealed class ThrowAfterSetDesignerClipboard : IDesignerClipboard
    {
        public event EventHandler? Changed;

        public string? Content { get; private set; }

        public void SetContent(string content)
        {
            Content = content;
            throw new InvalidOperationException("Expected clipboard notification failure.");
        }

        public void Clear()
        {
            Content = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
