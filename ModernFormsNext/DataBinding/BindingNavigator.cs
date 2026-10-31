using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;
using ModernFormsNext.Layout;
using ModernFormsNext.WindowKit.Input;

namespace ModernFormsNext.DataBinding;

/// <summary>Provides a framework-native navigation and editing bar for a borrowed BindingSource.</summary>
/// <remarks>
/// Use on the owning UI thread, including BindingSource notifications. All presentation children
/// belong to the existing control tree; layout, RTL, focus, validation, themes and accessibility
/// use the ordinary framework services. The source and application commands are never disposed.
/// Default Save calls BindingSource.EndEdit, and Cancel calls BindingSource.CancelEdit; neither
/// performs persistence or tracks an application dirty flag. Replace a button's Command to supply
/// application policy, including its CanExecute and parameter. Local Enabled and Visible intent
/// is retained. Sources/commands/owned children are runtime-only, not .mfdesign serialized objects.
/// Cancel opts out of focus validation so an invalid draft can be discarded through the existing
/// validation contract. Customize the owned children without disposing or reparenting them.
/// </remarks>
/// <example><code>
/// var people = new BindingSource { DataSource = new BindingList&lt;Person&gt;() };
/// var navigator = new BindingNavigator(people) { Dock = DockStyle.Top };
/// var save = new DelegateCommand(() =&gt;
/// {
///     people.EndEdit();
///     SavePeople(); // Application-owned persistence.
/// }, () =&gt; people.Count &gt; 0);
/// navigator.SaveButton.Command = save;
/// people.ListChanged += (_, _) =&gt; save.RaiseCanExecuteChanged();
/// // The application detaches its handlers/disposes its source with its own lifetime.
/// form.Controls.Add(navigator);
/// </code></example>
[DefaultProperty(nameof(CountItemFormat)), DefaultEvent(nameof(BindingSourceChanged))]
public partial class BindingNavigator : FlowLayoutPanel, ISupportInitialize
{
    private readonly List<DelegateCommand> standardCommands = [];
    private BindingSource? bindingSource;
    private long revision;
    private long sourceAssignmentVersion;
    private int initializationDepth;
    private bool retiring;
    private string countItemFormat = "of {0}";

    /// <summary>Creates a bar with standard navigation/edit actions and no bound source.</summary>
    public BindingNavigator()
    {
        SuspendLayout();
        TabStop = false;
        Dock = DockStyle.Top;
        WrapContents = false;
        AutoScroll = true;
        Padding = new Padding(4);
        AccessibleRole = AccessibleRole.ToolBar;
        MoveFirstButton = AddAction("MoveFirst", "First", Operation.First);
        MovePreviousButton = AddAction("MovePrevious", "Previous", Operation.Previous);
        PositionTextBox = Controls.AddImplicitControl(new PositionEditor(this)
        {
            Name = "Position", AccessibleName = "Position", Width = 60, Height = 30,
            MaxLength = 32, Margin = new Padding(2)
        });
        CountLabel = Controls.AddImplicitControl(new Label
        {
            Name = "Count", AutoSize = true, MinimumSize = new Size(0, 30),
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(2), TabStop = false
        });
        MoveNextButton = AddAction("MoveNext", "Next", Operation.Next);
        MoveLastButton = AddAction("MoveLast", "Last", Operation.Last);
        AddNewButton = AddAction("AddNew", "Add", Operation.Add);
        DeleteButton = AddAction("Delete", "Delete", Operation.Delete);
        SaveButton = AddAction("Save", "Save", Operation.Save);
        CancelButton = AddAction("Cancel", "Cancel", Operation.Cancel);
        PositionTextBox.Validated += PositionValidated;
        ResumeLayout(true);
        RefreshItems();
    }

    /// <summary>Creates a standard bar attached to a borrowed source, which may be null.</summary>
    /// <param name="bindingSource">The source to navigate. Its lifetime remains with the caller.</param>
    public BindingNavigator(BindingSource? bindingSource) : this() => BindingSource = bindingSource;

    /// <summary>Gets or sets the borrowed source; null displays position/count zero and disables default actions.</summary>
    /// <remarks>
    /// Replacement detaches old notifications, refreshes presentation and then raises BindingSourceChanged.
    /// Disposing the source detaches it. No source/list is copied, owned or persisted. Notifications
    /// must arrive on the UI thread. After suppressed list notifications, call RefreshItems explicitly.
    /// This reference is runtime-only; assign it after InitializeComponent for Designer-created bars.
    /// </remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BindingSource? BindingSource
    {
        get => bindingSource;
        set
        {
            VerifyAccess();
            if (ReferenceEquals(bindingSource, value)) return;
            UnwireSource();
            bindingSource = value;
            long assignment = ++sourceAssignmentVersion;
            ++revision;
            if (value is not null)
            {
                value.PositionChanged += SourceChanged;
                value.CurrentChanged += SourceChanged;
                value.ListChanged += SourceChanged;
                value.DataSourceChanged += SourceChanged;
                value.DataMemberChanged += SourceChanged;
                value.Disposed += SourceDisposed;
            }
            RefreshItems();
            if (!retiring && !IsDisposed && assignment == sourceAssignmentVersion && ReferenceEquals(bindingSource, value))
                OnBindingSourceChanged(EventArgs.Empty);
        }
    }

    /// <summary>Occurs after source replacement and a successful presentation refresh.</summary>
    /// <remarks>Raised synchronously on the UI thread. Reentrant replacement owns the newer state.</remarks>
    public event EventHandler? BindingSourceChanged;

    /// <summary>Gets or sets the localized literal count template; each {0} is replaced by the count.</summary>
    /// <remarks>
    /// Defaults to "of {0}". Only the exact {0} token is supported; alignment, format specifiers and
    /// other brace expressions are rejected. This data-only replacement has bounded expansion and
    /// is safe for inert Designer previews. Changes update text and ordinary child layout.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The template is null.</exception>
    /// <exception cref="ArgumentException">A brace expression is not the exact {0} token.</exception>
    [DefaultValue("of {0}"), Localizable(true)]
    public string CountItemFormat
    {
        get => countItemFormat;
        set
        {
            VerifyAccess();
            ArgumentNullException.ThrowIfNull(value);
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '{' && value.AsSpan(index).StartsWith("{0}")) { index += 2; continue; }
                if (value[index] is '{' or '}')
                    throw new ArgumentException("The count template supports literal text and the exact {0} token only.", nameof(value));
            }
            if (countItemFormat == value) return;
            countItemFormat = value;
            RefreshItems();
        }
    }

    /// <summary>Gets the owned First button. Its default command moves to position zero.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button MoveFirstButton { get; }
    /// <summary>Gets the owned Previous button. Its default command moves back one item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button MovePreviousButton { get; }
    /// <summary>Gets the owned, one-based position editor. Enter commits, Escape restores; validation uses the normal editor lifecycle.</summary>
    /// <remarks>Invalid text restores the current position. Integer input is clamped to the current non-empty list.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TextBox PositionTextBox { get; }
    /// <summary>Gets the owned count label. Configure CountItemFormat to localize its automatic text.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Label CountLabel { get; }
    /// <summary>Gets the owned Next button. Its default command advances one item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button MoveNextButton { get; }
    /// <summary>Gets the owned Last button. Its default command moves to the last item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button MoveLastButton { get; }
    /// <summary>Gets the owned Add button. Its default command calls BindingSource.AddNew when AllowNew is true.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button AddNewButton { get; }
    /// <summary>Gets the owned Delete button. Its default command calls BindingSource.RemoveCurrent when AllowRemove is true.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button DeleteButton { get; }
    /// <summary>Gets the owned Save button. Its default command calls EndEdit for an editable or addable source's current item, with no persistence.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button SaveButton { get; }
    /// <summary>Gets the owned Cancel button. Its default command calls CancelEdit for an editable or addable source's current item.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Button CancelButton { get; }

    /// <summary>Defers presentation changes until the matching final EndInit call.</summary>
    /// <remarks>Calls may nest. Source ownership/subscriptions still change immediately.</remarks>
    public void BeginInit() { VerifyAccess(); initializationDepth++; ++revision; }

    /// <summary>Ends one initialization scope and refreshes after the outermost scope.</summary>
    /// <exception cref="InvalidOperationException">There is no matching BeginInit call.</exception>
    public void EndInit()
    {
        VerifyAccess();
        if (initializationDepth == 0) throw new InvalidOperationException("EndInit requires a matching BeginInit.");
        if (--initializationDepth == 0) RefreshItems();
    }

    /// <summary>Refreshes displayed currency and availability of the original standard commands.</summary>
    /// <remarks>
    /// UI thread only. Does not move currency or execute an edit action. Replacing a button's Command
    /// gives the application control of that action's availability; its command notifications continue
    /// through the canonical CommandSource. Local Enabled/Visible intent is never overwritten.
    /// Observer errors propagate after remaining still-current standard items have been reconciled.
    /// A source/action failure is never treated as success or retried.
    /// </remarks>
    public void RefreshItems()
    {
        VerifyAccess();
        if (initializationDepth != 0) return;
        long stamp = ++revision;
        var source = bindingSource;
        int count = source?.Count ?? 0;
        if (!IsCurrent(stamp, source)) return;
        int position = source?.Position ?? -1;
        if (!IsCurrent(stamp, source)) return;
        List<Exception>? failures = null;
        Apply(() => PositionTextBox.SetCommandEnabled(count > 0), ref failures);
        if (IsCurrent(stamp, source)) Apply(() => PositionTextBox.Text = (count == 0 ? 0 : position + 1).ToString(CultureInfo.CurrentCulture), ref failures);
        if (IsCurrent(stamp, source)) Apply(() => CountLabel.Text = countItemFormat.Replace("{0}", count.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal), ref failures);
        foreach (var command in standardCommands)
        {
            if (!IsCurrent(stamp, source)) break;
            Apply(command.RaiseCanExecuteChanged, ref failures);
        }
        ThrowFailures(failures);
    }

    /// <summary>Raises BindingSourceChanged after the current source has been refreshed.</summary>
    /// <param name="e">The event data.</param>
    /// <remarks>Runs on the UI thread. Overrides must call base.</remarks>
    protected virtual void OnBindingSourceChanged(EventArgs e) => BindingSourceChanged?.Invoke(this, e);

    /// <inheritdoc/>
    protected override Size DefaultSize => new(760, 42);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // The inherited finalizer must not enumerate children, touch focus or detach managed events.
        if (!disposing) { base.Dispose(false); return; }
        if (retiring) return;
        retiring = true;
        ++revision;
        ++sourceAssignmentVersion;
        List<Exception>? failures = null;
        Apply(UnwireSource, ref failures);
        bindingSource = null;
        // A borrowed command may throw from event detachment. Finish the still-owned canonical
        // tree before base cleanup so that one button cannot strand the position editor or IME.
        foreach (var child in Controls.GetAllControls(true).ToArray())
        {
            if (ReferenceEquals(child.Parent, this)) Apply(child.Dispose, ref failures);
        }
        Apply(() => base.Dispose(true), ref failures);
        ThrowFailures(failures);
    }

    private Button AddAction(string name, string text, Operation operation)
    {
        var command = new DelegateCommand(() => ExecuteOperation(operation), () => TryGetEligibleSource(operation, out _));
        standardCommands.Add(command);
        return Controls.AddImplicitControl(new Button
        {
            Name = name, Text = text, Command = command, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(0, 30),
            CausesValidation = operation != Operation.Cancel,
            Padding = new Padding(8, 2, 8, 2), Margin = new Padding(2)
        });
    }

    private bool TryGetEligibleSource(Operation operation, out BindingSource? source)
    {
        source = bindingSource;
        if (retiring || IsDisposed || Disposing || initializationDepth != 0 || source is null) return false;
        GetFocusScope().VerifyAccess();
        long stamp = revision;
        int count = source.Count;
        if (!IsCurrent(stamp, source)) return false;
        int position = source.Position;
        if (!IsCurrent(stamp, source)) return false;
        bool current = count > 0 && position >= 0 && position < count;
        bool eligible = operation switch
        {
            Operation.First => count > 0 && position != 0,
            Operation.Previous => position > 0,
            Operation.Next => count > 0 && position < count - 1,
            Operation.Last => count > 0 && position != count - 1,
            Operation.Add => source.AllowNew,
            Operation.Delete => current && source.AllowRemove,
            // Pending additions also need EndEdit/CancelEdit when existing rows are read-only.
            Operation.Save or Operation.Cancel => current && (source.AllowEdit || source.AllowNew),
            _ => false
        };
        return eligible && IsCurrent(stamp, source);
    }

    private void ExecuteOperation(Operation operation)
    {
        VerifyAccess();
        if (!TryGetEligibleSource(operation, out var source)) return;
        List<Exception>? failures = null;
        Apply(() =>
        {
            switch (operation)
            {
                case Operation.First: source!.MoveFirst(); break;
                case Operation.Previous: source!.MovePrevious(); break;
                case Operation.Next: source!.MoveNext(); break;
                case Operation.Last: source!.MoveLast(); break;
                case Operation.Add: source!.AddNew(); break;
                case Operation.Delete: source!.RemoveCurrent(); break;
                case Operation.Save: source!.EndEdit(); break;
                case Operation.Cancel: source!.CancelEdit(); break;
            }
        }, ref failures);
        // A callback may replace the source/dispose the bar. Never apply an old snapshot afterwards.
        if (!retiring && !IsDisposed && ReferenceEquals(bindingSource, source)) Apply(RefreshItems, ref failures);
        ThrowFailures(failures);
    }

    private void CommitPosition()
    {
        VerifyAccess();
        var source = bindingSource;
        long stamp = revision;
        string text = PositionTextBox.Text;
        List<Exception>? failures = null;
        if (source is not null && text.Length <= 32 && int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value))
        {
            int count = source.Count;
            if (count > 0 && IsCurrent(stamp, source))
                Apply(() => source.Position = Math.Clamp(value, 1, count) - 1, ref failures);
        }
        if (!retiring && !IsDisposed && ReferenceEquals(bindingSource, source)) Apply(RefreshItems, ref failures);
        ThrowFailures(failures);
    }

    private void PositionValidated(object? sender, EventArgs e) { if (!retiring && !IsDisposed) CommitPosition(); }
    private void SourceChanged(object? sender, EventArgs e) { if (!retiring && !IsDisposed && ReferenceEquals(sender, bindingSource)) RefreshItems(); }
    private void SourceDisposed(object? sender, EventArgs e) { if (!retiring && !IsDisposed && ReferenceEquals(sender, bindingSource)) BindingSource = null; }
    private bool IsCurrent(long stamp, BindingSource? source) => !retiring && !IsDisposed && !Disposing && stamp == revision && ReferenceEquals(source, bindingSource);

    private void UnwireSource()
    {
        if (bindingSource is not { } source) return;
        source.PositionChanged -= SourceChanged;
        source.CurrentChanged -= SourceChanged;
        source.ListChanged -= SourceChanged;
        source.DataSourceChanged -= SourceChanged;
        source.DataMemberChanged -= SourceChanged;
        source.Disposed -= SourceDisposed;
    }

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(retiring || IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    private static void Apply(Action action, ref List<Exception>? failures)
    {
        try { action(); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
    }

    private static void ThrowFailures(List<Exception>? failures)
    {
        if (failures is { Count: 1 }) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is { Count: > 1 }) throw new AggregateException("Navigator action or presentation observers failed.", failures);
    }

    private enum Operation { First, Previous, Next, Last, Add, Delete, Save, Cancel }

    private sealed class PositionEditor(BindingNavigator owner) : TextBox
    {
        protected override bool ProcessTextBoxKeyDown(KeyEventArgs e)
        {
            if (!owner.retiring && !owner.IsDisposed && !e.Control && !e.Alt && !document.HasComposition)
            {
                if (e.KeyCode == Keys.Enter) { owner.CommitPosition(); return true; }
                if (e.KeyCode == Keys.Escape) { owner.RefreshItems(); return true; }
            }
            return base.ProcessTextBoxKeyDown(e);
        }
    }
}
