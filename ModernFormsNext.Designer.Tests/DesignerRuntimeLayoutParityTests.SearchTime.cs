using System.Globalization;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.CodeGeneration.Reverse;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using SkiaSharp;
using Xunit;

namespace ModernFormsNext.Designer.Tests;

public sealed partial class DesignerRuntimeLayoutParityTests
{
    [Theory]
    [InlineData(nameof(SearchBar))]
    [InlineData(nameof(TimePicker))]
    public void SearchTimeSettingsAndFullTickValuesRoundTripThroughCanonicalDesigner(string type)
    {
        var document = BasicDocument();
        var node = Node("utility", type, 20, 25, 400, 36);
        document.Controls.Add(node);
        using var session = new DesignerSession();
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var expectedTime = new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(1_234_567));
        if (type == nameof(TimePicker))
        {
            Commit(grid, "MinimumTime", "09:00");
            Commit(grid, "MaximumTime", "17:00");
            Commit(grid, "Time", "13:14:15.1234567");
            Commit(grid, "Format", "HH:mm:ss.fffffff");
            Commit(grid, "CultureName", "en-GB");
            Commit(grid, "StepSeconds", "30");
            Assert.True(TimeOnlyDesignValue.TryRead(node.Properties["Time"], out var stored, out var error), error);
            Assert.Equal(expectedTime, stored);
        }
        else
        {
            Commit(grid, "Text", "Find entries");
            Commit(grid, "Placeholder", "Search");
            Commit(grid, "HistoryCapacity", "8");
            Commit(grid, "MaximumVisibleSuggestions", "4");
            Commit(grid, "SuggestionRowHeight", "40");
            Commit(grid, "SubmitOnSuggestion", "False");
            Commit(grid, "SearchButtonText", "Go");
        }
        Assert.DoesNotContain(grid.Properties, p => p.Name is "SuggestionProvider" or "SuggestionsSource" or
            "SuggestionItemTemplate" or "SuggestionTextSelector" or "SearchCommand" or "SearchCommandParameter" or
            "SearchCommandTarget" or "History" or "AreSuggestionsOpen" or "IsLoadingSuggestions" or "HighlightedSuggestionIndex");
        var reopened = DesignDocumentSerializer.Default.Deserialize(DesignDocumentSerializer.Default.Serialize(document));
        var generated = new CSharpDesignerGenerator().Generate(reopened);
        Assert.True(generated.Succeeded, string.Join(Environment.NewLine, generated.Validation.Errors));
        if (type == nameof(TimePicker)) Assert.Contains($"new global::System.TimeOnly({expectedTime.Ticks.ToString(CultureInfo.InvariantCulture)}L)", generated.Code);
        var parsed = new CSharpDesignerParser().Parse(generated.Code, new CSharpDesignerParseOptions
        { RootKind = reopened.RootKind, NamespaceOverride = reopened.Namespace, ClassNameOverride = reopened.ClassName });
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        using var runtime = CompileAndCreateGeneratedRoot(parsed.Document!);
        var root = Assert.IsAssignableFrom<Control>(runtime.Instance);
        var utility = Assert.Single(root.Controls);
        Assert.Empty(utility.Controls);
        if (utility is TimePicker time)
        {
            Assert.Equal(expectedTime, time.Time);
            Assert.Equal(new TimeOnly(9, 0), time.MinimumTime);
            Assert.Equal(new TimeOnly(17, 0), time.MaximumTime);
            Assert.Equal(30, time.StepSeconds);
            Assert.Equal("13:14:15.1234567", time.Text);
            Assert.Equal("en-GB", time.CultureName);
        }
        else
        {
            var search = Assert.IsType<SearchBar>(utility);
            Assert.Equal("Find entries", search.Text);
            Assert.Equal(8, search.HistoryCapacity);
            Assert.Equal(4, search.MaximumVisibleSuggestions);
            Assert.Equal(40, search.SuggestionRowHeight);
            Assert.False(search.SubmitOnSuggestion);
            Assert.Equal("Go", search.SearchButtonText);
            Assert.Null(search.SuggestionProvider);
            Assert.Null(search.SearchCommand);
            Assert.False(search.AreSuggestionsOpen);
        }
    }

    [Theory]
    [InlineData(nameof(TimePicker), "StepSeconds", "0")]
    [InlineData(nameof(TimePicker), "StepSeconds", "86401")]
    [InlineData(nameof(TimePicker), "Format", "yyyy-MM-dd")]
    [InlineData(nameof(TimePicker), "CultureName", "not_a_culture!")]
    [InlineData(nameof(SearchBar), "HistoryCapacity", "1001")]
    [InlineData(nameof(SearchBar), "SuggestionRowHeight", "0")]
    [InlineData(nameof(SearchBar), "MaximumVisibleSuggestions", "21")]
    public void InvalidSearchTimeSettingRollsBackCanonicalGridTransaction(string type, string property, string invalid)
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("utility", type, 0, 0, 400, 36);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        var descriptor = Assert.Single(grid.Properties, p => p.Name == property);
        grid.SelectRow(new DesignerPropertyGridRow(descriptor));
        Assert.False(grid.CommitSelectedValue(invalid));
        Assert.False(node.Properties.ContainsKey(property));
        Assert.False(session.Transactions.CanUndo);
    }

    [Fact]
    public void TimeRangeAndNullUseProductionValidationAndCanonicalUndo()
    {
        using var session = new DesignerSession();
        var document = BasicDocument();
        var node = Node("time", nameof(TimePicker), 0, 0, 260, 36);
        document.Controls.Add(node);
        session.LoadDocument(document);
        session.SelectNode(node);
        var grid = new DesignerPropertyGridState(session);
        Commit(grid, "MaximumTime", "17:00");
        Commit(grid, "MinimumTime", "09:00");
        var minimum = Assert.Single(grid.Properties, p => p.Name == "MinimumTime");
        grid.SelectRow(new DesignerPropertyGridRow(minimum));
        Assert.False(grid.CommitSelectedValue("18:00"));
        Commit(grid, "Time", "13:14:15.1234567");
        Assert.True(session.Transactions.Undo());
        Assert.False(node.Properties.ContainsKey("Time"));
        Assert.True(session.Transactions.Redo());
        Commit(grid, "Time", "null");
        Assert.Equal(DesignPropertyValueKind.Null, node.Properties["Time"].Kind);
    }

    [Fact]
    public void TimeOnlyDataCodecRejectsFractionalOutOfRangeAndUnknownMembers()
    {
        foreach (double ticks in new[] { -1d, 0.5d, TimeSpan.TicksPerDay, double.NaN, double.PositiveInfinity })
        {
            var invalid = DesignPropertyValue.FromStructuredObject("System.TimeOnly", new Dictionary<string, DesignPropertyValue>
            { ["Ticks"] = DesignPropertyValue.FromDouble(ticks) });
            Assert.False(TimeOnlyDesignValue.TryRead(invalid, out _, out _));
        }
        var extra = DesignPropertyValue.FromStructuredObject("System.TimeOnly", new Dictionary<string, DesignPropertyValue>
        { ["Ticks"] = DesignPropertyValue.FromDouble(1), ["Execute"] = DesignPropertyValue.FromString("project code") });
        Assert.False(TimeOnlyDesignValue.TryRead(extra, out _, out _));
        var maximum = TimeOnlyDesignValue.FromTime(TimeOnly.MaxValue);
        Assert.True(TimeOnlyDesignValue.TryRead(maximum, out var time, out _));
        Assert.Equal(TimeOnly.MaxValue, time);
    }

    [Theory]
    [InlineData("new global::System.TimeOnly(System.DateTime.Now.Ticks)")]
    [InlineData("new global::System.TimeOnly(GetTicks())")]
    [InlineData("System.TimeOnly.Parse(\"13:14\")")]
    [InlineData("new global::System.TimeOnly(-1L)")]
    [InlineData("new global::System.TimeOnly(864000000000L)")]
    [InlineData("new TimeOnly(1L)")]
    [InlineData("new global::System.TimeOnly(1L) { Unknown = GetTicks() }")]
    public void TimeOnlyParserRejectsExecutableInvalidAndAmbiguousExpressions(string expression)
    {
        var document = BasicDocument();
        var node = Node("time", nameof(TimePicker), 0, 0, 260, 36);
        node.Properties["Time"] = TimeOnlyDesignValue.FromTime(TimeOnly.MinValue);
        document.Controls.Add(node);
        var generated = new CSharpDesignerGenerator().Generate(document);
        Assert.True(generated.Succeeded);
        const string literal = "new global::System.TimeOnly(0L)";
        Assert.Contains(literal, generated.Code);
        var hostile = generated.Code.Replace(literal, expression, StringComparison.Ordinal);
        var parsed = new CSharpDesignerParser().Parse(hostile);
        Assert.NotEmpty(parsed.Diagnostics);
        Assert.True(parsed.Document is null || parsed.Document.Controls.All(control => !control.Properties.ContainsKey("Time")));
    }

    [Theory]
    [InlineData(nameof(SearchBar), 1d)]
    [InlineData(nameof(SearchBar), 1.5d)]
    [InlineData(nameof(SearchBar), 2.625d)]
    [InlineData(nameof(TimePicker), 1d)]
    [InlineData(nameof(TimePicker), 1.5d)]
    [InlineData(nameof(TimePicker), 2.625d)]
    public void SearchTimePreviewPaintsRealImplicitControlsAndDisposesThem(string type, double scale)
    {
        using Control control = type == nameof(SearchBar)
            ? new SearchBar { Text = "Preview query", Placeholder = "Search" }
            : new TimePicker { Time = new(13, 14, 15), Format = "HH:mm:ss" };
        // Accessibility intentionally hides unhosted controls. Use the real shared surface
        // to observe actual owned child identities before painting and disposal.
        using var surface = new SkiaControlSurface(control);
        surface.Resize(400, 36);
        control.Size = new(400, 36);
        control.PerformLayout();
        var root = control.AccessibilityObject;
        var children = Enumerable.Range(0, root.GetChildCount()).Select(root.GetChild)
            .OfType<Control.ControlAccessibleObject>().Select(peer => peer.Owner).Where(owner => owner is not null).ToArray();
        Assert.Equal(type == nameof(SearchBar) ? 3 : 1, children.Length);
        int disposed = 0;
        foreach (var child in children) child!.Disposed += (_, _) => disposed++;
        var info = new SKImageInfo((int)(400 * scale), (int)(36 * scale));
        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);
        Assert.True(RuntimeControlPainter.TryPaint(new PaintEventArgs(info, canvas, scale), control, control.Size,
            new System.Drawing.Rectangle(0, 0, info.Width, info.Height), out var diagnostics, out var error), error);
        Assert.True(diagnostics.VisibleSampleCount > 0);
        control.Dispose();
        Assert.Equal(children.Length, disposed);
    }
}
