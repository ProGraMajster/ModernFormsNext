using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Virtualization;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.Validation;

/// <summary>Finite shared assertions driven by separate actual Windows and Android input runners.</summary>
/// <remarks>UI thread only. Borrowed row references are inspected only during the current preparation epoch.</remarks>
internal sealed class SearchTimeCertification : IDisposable
{
    private readonly Form form;
    private readonly Action<string> output;
    private readonly Panel root;
    private readonly Rows template;
    private readonly string[] source = Enumerable.Range(0, 100_003).Select(index => $"Entry {index}").ToArray();
    private int executions;
    private ITextInputClient? originalClient;
    internal SearchBar Search { get; }
    internal TimePicker Time { get; }
    internal CollectionView View => (CollectionView)template.Live.First(row => row.Parent is CollectionView).Parent!;

    internal SearchTimeCertification(Form form, Action<string> output)
    {
        this.form = form;
        this.output = output;
        root = form.Controls.Add(new Panel { Dock = DockStyle.Fill });
        Search = root.Controls.Add(new SearchBar { Bounds = new(8, 8, 320, 36), AccessibleName = "Native search" });
        Time = root.Controls.Add(new TimePicker { Bounds = new(8, 76, 300, 36), CultureName = "pl-PL", Format = "HH:mm:ss.fffffff", StepSeconds = 30 });
        template = new Rows(source);
        Search.SuggestionItemTemplate = template;
        Search.SuggestionProvider = _ => source;
        Search.SearchCommand = new DelegateCommand(_ => executions++);
        root.BringToFront();
    }

    internal void FocusSearch()
    {
        Check(Search.FocusEditor(), "focus-real-editor");
        originalClient = form.TextInputClient;
        Check(originalClient is not null, "parent-text-client");
    }

    internal void VerifyText(string expected)
    {
        Check(Search.Text == expected, "native-text-" + expected.Replace(' ', '-'));
        Check(ReferenceEquals(originalClient, form.TextInputClient), "popup-retains-native-text-client");
    }

    internal void VerifySuggestions()
    {
        Check(Search.AreSuggestionsOpen && Search.SuggestionsSource?.Count == source.Length, "native-large-source-popup");
        var snapshot = View.GetVirtualizationSnapshot();
        Check(snapshot.RealizedContainerCount is > 0 and <= 16, "native-bounded-suggestions");
        Check(!View.AccessibilityObject.ScrollInfo!.Value.Horizontal.IsScrollable, "native-suggestions-no-horizontal-overflow");
        output($"SEARCH_TIME rows={snapshot.RealizedContainerCount} pool={snapshot.RecycledContainerCount} scale={Search.ScaleFactor}");
    }

    internal void ChooseWithKeys(Action<Keys> nativeKey)
    {
        nativeKey(Keys.Down);
        Check(Search.HighlightedSuggestionIndex == 0, "native-highlight");
        nativeKey(Keys.Enter);
    }

    internal void VerifyCommit(string text, int count)
    {
        Check(Search.Text == text && executions == count && !Search.AreSuggestionsOpen, "native-suggestion-commit-" + count);
        Check(Search.History.FirstOrDefault() == text, "native-memory-history-" + count);
    }

    internal void PreparePointer() { Search.Text = "Entry"; FocusSearch(); Search.RefreshSuggestions(); }
    internal Point SuggestionPoint()
    {
        var bounds = View.AccessibilityObject.GetChild(1)!.Bounds;
        return new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }

    internal void ExerciseTimeKey(Action<Keys> nativeKey)
    {
        Time.Time = new TimeOnly(14, 30, 15).Add(TimeSpan.FromTicks(1234567));
        Check(Time.FocusEditor(), "time-focus-real-spinner");
        nativeKey(Keys.Up);
        Check(Time.Time == new TimeOnly(14, 30, 45).Add(TimeSpan.FromTicks(1234567)), "native-time-key-full-precision");
    }

    internal Point TimeIncreasePoint()
    {
        var bounds = Time.AccessibilityObject.GetChild(0)!.GetChild(1)!.Bounds;
        return new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
    }

    internal void VerifyTimePointer()
    {
        output($"SEARCH_TIME pointer-time={Time.Time:HH:mm:ss.fffffff} spinner={Time.AccessibilityObject.GetChild(0)!.Bounds} increase={TimeIncreasePoint()}");
        Check(Time.Time == new TimeOnly(14, 31, 15).Add(TimeSpan.FromTicks(1234567)), "native-time-pointer");
    }

    internal void VerifyTimeGeometry()
    {
        var spinner = Time.AccessibilityObject.GetChild(0)!;
        Check(spinner.ControlType == AccessibleControlType.Spinner && spinner.Bounds.Width > 0, "native-time-peer-bounds");
        Check(spinner.PerformAction(AccessibleActions.SetValue, "16:05:06.1234567") && Time.Time?.Ticks == new TimeOnly(16, 5, 6).Add(TimeSpan.FromTicks(1234567)).Ticks, "native-time-accessible-parser");
        Check(spinner.PerformAction(AccessibleActions.Toggle) && Time.Time is null, "native-time-null");
        Time.MinimumTime = new(9, 0);
        Time.MaximumTime = new(18, 0);
        Check(Time.Time is null, "native-null-range-preserved");
        Time.Time = new(16, 5, 6);
        Time.CultureName = "en-US";
        Time.Format = "hh:mm:ss tt";
        Check(Time.Text == "04:05:06 PM", "native-time-12-hour");
        Time.CultureName = "pl-PL";
        Time.Format = "HH:mm:ss.fffffff";
    }

    internal void Check(bool condition, string category)
    {
        if (!condition) throw new InvalidOperationException("SEARCH_TIME " + category);
        output("PASS search-time-" + category);
    }

    public void Dispose()
    {
        int disposed = 0;
        Search.Disposed += (_, _) => disposed++;
        Time.Disposed += (_, _) => disposed++;
        root.Parent?.Controls.Remove(root);
        root.Dispose();
        Check(disposed == 2 && template.Live.Count == 0, "owned-controls-and-rows-disposed");
    }

    private sealed class Rows(string[] source) : IItemContainerFactory<Control>
    {
        internal HashSet<Control> Live { get; } = [];
        public Control CreateContainer() { var row = new Label(); Live.Add(row); return row; }
        public void PrepareContainer(Control container, int index) => container.Text = source[index];
        public void ClearContainer(Control container, int previousIndex) => container.Text = string.Empty;
        public void DiscardContainer(Control container) { Live.Remove(container); container.Dispose(); }
    }
}
