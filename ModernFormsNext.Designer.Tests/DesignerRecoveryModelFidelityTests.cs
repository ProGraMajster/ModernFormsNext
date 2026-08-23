using System.Drawing;
using System.Numerics;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Recovery;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.Drawing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed class DesignerRecoveryModelFidelityTests
{
    private static readonly DateTimeOffset RecoveryTimestamp = new(
        2026,
        8,
        23,
        12,
        0,
        0,
        TimeSpan.Zero);

    private static readonly DesignerRecoverySessionIdentity RecoverySession = new(
        Guid.Parse("cf156205-d16f-45ff-8a2f-fc0f5c71d762"),
        processId: 4141);

    [Fact]
    public void RecoveryDiscoveryAndRestoreNeverExecuteResolvableCustomUserControlConstructor()
    {
        RecoveryConstructorTrapUserControl.ConstructorCalls = 0;
        var recovered = CreateDocument("RecoveredConstructorProbe", DesignRootKind.Form);
        recovered.Controls.Add(new DesignControlNode
        {
            TypeName = typeof(RecoveryConstructorTrapUserControl).AssemblyQualifiedName!,
            Name = "constructorTrap1",
            Bounds = new DesignBounds(24, 32, 220, 120),
            Properties =
            {
                ["Text"] = DesignPropertyValue.FromString("Data-only recovery")
            }
        });

        using var context = new RecoveryRestoreContext(recovered, "ConstructorProbe.mfdesign");

        Assert.Equal(0, RecoveryConstructorTrapUserControl.ConstructorCalls);
        context.Restore();
        RenderRecoveredDocument(context.Session);

        Assert.Equal(0, RecoveryConstructorTrapUserControl.ConstructorCalls);
        Assert.Equal(
            typeof(RecoveryConstructorTrapUserControl).AssemblyQualifiedName,
            Assert.Single(context.Session.Document.Controls).TypeName);
        Assert.True(context.Session.IsDirty);
        Assert.False(context.Session.Transactions.CanUndo);
    }

    [Fact]
    public void RecoveryRestorePreservesComplexShapesBrushesEffectsTransitionsMetadataAndCollections()
    {
        var source = CreateComplexRecoveryDocument();
        var expectedJson = DesignDocumentSerializer.Default.Serialize(source);
        using var context = new RecoveryRestoreContext(source, "ComplexRecovery.mfdesign");

        context.Restore();

        var restored = context.Session.Document;
        Assert.Equal(expectedJson, DesignDocumentSerializer.Default.Serialize(restored));
        Assert.Equal("Issue41.RecoveryFidelity", restored.Metadata.ToolName);
        Assert.Equal("OnRecoveredRootLoaded", restored.Events["Load"]);
        Assert.Equal(
            "preserve-this-metadata",
            restored.Properties["RecoveryMetadata"].ObjectProperties!["Token"].GetString());

        var panel = Assert.Single(restored.Controls);
        var path = Assert.Single(panel.Children, node => node.TypeName == nameof(ModernFormsNext.Path));
        Assert.Equal("OnRecoveredPathClick", path.Events["Click"]);

        var fill = Assert.IsType<LinearGradientBrush>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(
                path.Properties["Fill"],
                typeof(ModernFormsNext.Drawing.Brush)));
        Assert.Equal(new PointF(0.125f, 0.25f), fill.Start);
        Assert.Equal(new PointF(0.875f, 0.75f), fill.End);
        Assert.Equal(Matrix3x2.CreateTranslation(1.5f, -2.25f), fill.Transform);
        Assert.Collection(
            fill.GradientStops,
            stop =>
            {
                Assert.Equal(Color.CornflowerBlue.ToArgb(), stop.PaintColor.ToArgb());
                Assert.Equal(0.125f, stop.Offset);
            },
            stop =>
            {
                Assert.Equal(Color.Gold.ToArgb(), stop.PaintColor.ToArgb());
                Assert.Equal(0.625f, stop.Offset);
            },
            stop =>
            {
                Assert.Equal(Color.DarkRed.ToArgb(), stop.PaintColor.ToArgb());
                Assert.Equal(1f, stop.Offset);
            });

        var geometry = Assert.IsType<PathGeometry>(
            DesignerPropertyValueEditor.FromDesignPropertyValue(
                path.Properties["Data"],
                typeof(Geometry)));
        Assert.Equal(GeometryFillRule.EvenOdd, geometry.FillRule);
        Assert.Equal(2, geometry.Figures.Count);
        Assert.Collection(
            geometry.Figures[0].Segments,
            segment => Assert.IsType<LineSegment>(segment),
            segment => Assert.IsType<QuadraticBezierSegment>(segment),
            segment => Assert.IsType<BezierSegment>(segment));
        Assert.True(geometry.Figures[0].IsClosed);
        Assert.False(geometry.Figures[1].IsClosed);

        Assert.True(InteractionEffectDesignValue.TryRead(
            path.Properties[InteractionEffectDesignValue.PropertyName],
            out var effects,
            out var effectError), effectError);
        Assert.Collection(
            effects,
            effect => Assert.EndsWith("RippleEffect", effect.ObjectTypeName, StringComparison.Ordinal),
            effect => Assert.EndsWith("PressScaleEffect", effect.ObjectTypeName, StringComparison.Ordinal));

        Assert.True(LayoutTransitionDesignValue.TryRead(
            path.Properties[LayoutTransitionDesignValue.PropertyName],
            out var layoutEnabled,
            out var layoutDuration,
            out var layoutEasing,
            out var layoutError), layoutError);
        Assert.True(layoutEnabled);
        Assert.Equal(275.5d, layoutDuration);
        Assert.Equal("EaseOut", layoutEasing);

        Assert.True(VisualStateTransitionDesignValue.TryRead(
            path.Properties[VisualStateTransitionDesignValue.PropertyName],
            out var transitions,
            out var transitionError), transitionError);
        Assert.Collection(
            transitions,
            transition =>
            {
                Assert.Equal("Normal", transition.From);
                Assert.Equal("Hover", transition.To);
                Assert.Equal(90d, transition.DurationMilliseconds);
            },
            transition =>
            {
                Assert.Equal("Hover", transition.From);
                Assert.Equal("Pressed", transition.To);
                Assert.Equal(120d, transition.DurationMilliseconds);
            });

        Assert.True(context.Session.IsDirty);
        Assert.False(context.Session.Transactions.CanUndo);
    }

    [Fact]
    public void RepresentativeRecoveredDocumentRetainsDesignerRuntimeLayoutParity()
    {
        var runtimeSource = CreateParityRecoveryDocument();
        using var context = new RecoveryRestoreContext(runtimeSource, "ParityRecovery.mfdesign");

        context.Restore();

        DesignerRuntimeLayoutParityHarness.AssertParity(
            "issue-41-recovery-restore",
            context.Session.Document,
            runtimeSource,
            runtimeSource.Size);
        Assert.True(context.Session.IsDirty);
        Assert.False(context.Session.Transactions.CanUndo);
    }

    private static DesignDocument CreateComplexRecoveryDocument()
    {
        var document = CreateDocument("ComplexRecoveryForm", DesignRootKind.Form);
        document.Metadata.ToolName = "Issue41.RecoveryFidelity";
        document.Properties["Padding"] = ToDesignValue(new Padding(7, 11, 13, 17), typeof(Padding));
        document.Properties["RecoveryMetadata"] = DesignPropertyValue.FromStructuredObject(
            "Example.RecoveryMetadata",
            new SortedDictionary<string, DesignPropertyValue>(StringComparer.Ordinal)
            {
                ["Revision"] = DesignPropertyValue.FromInt32(42),
                ["Token"] = DesignPropertyValue.FromString("preserve-this-metadata")
            });
        document.Events["Load"] = "OnRecoveredRootLoaded";

        var panel = Node("contentPanel", "Panel", 0, 0, 760, 520, DockStyle.Fill);
        panel.Properties["Padding"] = ToDesignValue(new Padding(9), typeof(Padding));
        var path = Node("recoveredPath", nameof(ModernFormsNext.Path), 32, 44, 300, 220);
        path.Events["Click"] = "OnRecoveredPathClick";

        var fill = new LinearGradientBrush
        {
            Start = new PointF(0.125f, 0.25f),
            End = new PointF(0.875f, 0.75f),
            Transform = Matrix3x2.CreateTranslation(1.5f, -2.25f)
        };
        fill.GradientStops.AddRange([
            new GradientStop(Color.CornflowerBlue, 0.125f),
            new GradientStop(Color.Gold, 0.625f),
            new GradientStop(Color.DarkRed, 1f)
        ]);
        path.Properties["Fill"] = ToDesignValue(fill, typeof(ModernFormsNext.Drawing.Brush));
        path.Properties["Stroke"] = ToDesignValue(
            new SolidColorBrush(Color.Navy),
            typeof(ModernFormsNext.Drawing.Brush));
        path.Properties["StrokeThickness"] = DesignPropertyValue.FromDouble(2.75d);
        path.Properties["Data"] = ToDesignValue(CreateComplexPathGeometry(), typeof(Geometry));
        path.Properties[InteractionEffectDesignValue.PropertyName] =
            InteractionEffectDesignerRegistry.WriteCollection([
                InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.RippleTypeName),
                InteractionEffectDesignerRegistry.Create(InteractionEffectDesignerRegistry.PressScaleTypeName)
            ]);
        path.Properties[LayoutTransitionDesignValue.PropertyName] =
            LayoutTransitionDesignValue.Create(enabled: true, durationMilliseconds: 275.5d, easing: "EaseOut");
        path.Properties[VisualStateTransitionDesignValue.PropertyName] =
            VisualStateTransitionDesignValue.Create([
                new DesignVisualStateTransition("Normal", "Hover", 90d, "CubicOut"),
                new DesignVisualStateTransition("Hover", "Pressed", 120d, "EaseOut")
            ]);

        panel.Children.Add(path);
        panel.Children.Add(Node("statusLabel", "Label", 24, 460, 240, 28));
        document.Controls.Add(panel);
        return document;
    }

    private static DesignDocument CreateParityRecoveryDocument()
    {
        var document = CreateDocument("RecoveredParityControl", DesignRootKind.UserControl);
        document.Size = new DesignSize(300, 200);

        var card = Node("card", "UserControl", 10, 10, 280, 180);
        card.Properties["Padding"] = ToDesignValue(new Padding(8), typeof(Padding));
        var content = Node("content", "Panel", 0, 0, 100, 100, DockStyle.Fill);
        var anchored = Node("anchored", "Button", 150, 100, 90, 32);
        anchored.Properties["Anchor"] = EnumValue(AnchorStyles.Right | AnchorStyles.Bottom);

        content.Children.Add(anchored);
        card.Children.Add(content);
        document.Controls.Add(card);
        return document;
    }

    private static DesignDocument CreateDocument(string className, DesignRootKind rootKind)
        => new()
        {
            Namespace = "ModernFormsNext.RecoveryFidelity",
            ClassName = className,
            FormName = className,
            RootKind = rootKind,
            Size = new DesignSize(800, 600)
        };

    private static DesignControlNode Node(
        string name,
        string typeName,
        int x,
        int y,
        int width,
        int height,
        DockStyle dock = DockStyle.None)
    {
        var node = new DesignControlNode
        {
            Name = name,
            TypeName = typeName,
            Bounds = new DesignBounds(x, y, width, height)
        };
        node.Properties["Dock"] = EnumValue(dock);
        return node;
    }

    private static DesignPropertyValue EnumValue<T>(T value) where T : struct, Enum
        => DesignPropertyValue.FromEnum(typeof(T).FullName!, value.ToString());

    private static DesignPropertyValue ToDesignValue(object value, Type type)
        => DesignerPropertyValueEditor.ToDesignPropertyValue(value, type);

    private static PathGeometry CreateComplexPathGeometry()
    {
        var geometry = new PathGeometry
        {
            FillRule = GeometryFillRule.EvenOdd,
            Transform = new Matrix3x2(1.25f, 0.125f, -0.25f, 0.875f, 1.5f, -2.25f)
        };
        var first = new PathFigure(new PointF(1.5f, 2.25f), isClosed: true);
        first.Segments.Add(new LineSegment(new PointF(10.5f, 3.25f)));
        first.Segments.Add(new QuadraticBezierSegment(
            new PointF(12.5f, 5.75f),
            new PointF(16.25f, 9.5f)));
        first.Segments.Add(new BezierSegment(
            new PointF(18.5f, 11.25f),
            new PointF(21.75f, 13.5f),
            new PointF(24.25f, 17.75f)));
        var second = new PathFigure(new PointF(-3.5f, 4.25f));
        second.Segments.Add(new LineSegment(new PointF(5.5f, 8.75f)));
        geometry.Figures.Add(first);
        geometry.Figures.Add(second);
        return geometry;
    }

    private static void RenderRecoveredDocument(DesignerSession session)
    {
        const int width = 1000;
        const int height = 720;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        new DesignerSurfaceRenderer().Render(
            new PaintEventArgs(info, canvas, scaling: 1),
            session,
            width,
            height);
    }

    private sealed class RecoveryRestoreContext : IDisposable
    {
        private readonly TemporaryDirectory directory = new();

        public RecoveryRestoreContext(DesignDocument recoveredDocument, string suggestedName)
        {
            Store = new DesignerRecoveryStore(IOPath.Combine(directory.Path, "Recovery"));
            var snapshot = DesignerRecoverySnapshot.CaptureUnsaved(
                recoveredDocument,
                Guid.NewGuid(),
                suggestedName,
                projectPath: null,
                dirtyRevision: 7,
                revisionGeneration: 2,
                RecoverySession,
                RecoveryTimestamp);
            var write = Store.Write(snapshot);
            Assert.True(write.Succeeded, write.Error);

            Session = new DesignerSession();
            Session.OpenDocument(
                CreateDocument("PlaceholderForm", DesignRootKind.Form),
                path: null);
            Coordinator = new DesignerPersistenceCoordinator(
                Session,
                new DesignerFileService(currentDocumentPathProvider: () => Session.CurrentDocumentPath),
                new ModernFormsDesignerOptions
                {
                    AutoSaveEnabled = false,
                    AutoGenerateDesignerCodeOnSave = false
                },
                Store,
                new ManualDesignerOneShotScheduler(RecoveryTimestamp),
                InlineDesignerUiDispatcher.Instance,
                UnexpectedFileChangeSourceFactory.Instance,
                RecoverySession);
        }

        public DesignerRecoveryStore Store { get; }

        public DesignerSession Session { get; }

        public DesignerPersistenceCoordinator Coordinator { get; }

        public void Restore()
        {
            var notification = Assert.IsType<DesignerPersistenceNotification>(Coordinator.CurrentNotification);
            Assert.Equal(DesignerPersistenceNoticeKind.RecoveryAvailable, notification.Kind);
            Assert.True(Coordinator.ApplyCurrentAction(
                notification.Id,
                DesignerPersistenceActions.Restore,
                saveAsPath: null,
                out var error), error);
        }

        public void Dispose()
        {
            Coordinator.Dispose();
            Session.Dispose();
            directory.Dispose();
        }
    }

    private sealed class InlineDesignerUiDispatcher : IDesignerUiDispatcher
    {
        public static InlineDesignerUiDispatcher Instance { get; } = new();

        public void Post(Action callback)
            => callback();
    }

    private sealed class UnexpectedFileChangeSourceFactory : IDesignerFileChangeSourceFactory
    {
        public static UnexpectedFileChangeSourceFactory Instance { get; } = new();

        public IDesignerFileChangeSource Create(string designDocumentPath)
            => throw new InvalidOperationException("Unsaved recovery fidelity tests must not create file watchers.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = IOPath.Combine(
                IOPath.GetTempPath(),
                "ModernFormsNext.Designer.RecoveryFidelityTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

public sealed class RecoveryConstructorTrapUserControl : UserControl
{
    public static int ConstructorCalls { get; set; }

    public RecoveryConstructorTrapUserControl()
    {
        ConstructorCalls++;
        throw new InvalidOperationException("Recovery must not instantiate project UserControls.");
    }
}
