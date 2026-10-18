using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

/// <summary>Chooses an optional time through the existing date/time spinner and its canonical input and accessibility.</summary>
/// <remarks>
/// All operations require the owning UI thread. The owned implicit spinner is the sole value,
/// checked, range, rendering and focus engine. Its internal unspecified date carries only time
/// ticks; it has no time zone or daylight-saving meaning. Bounds are inclusive and never wrap
/// across midnight. Keyboard arrows, wheel and touch buttons step the value; the checkbox
/// activates or clears it. Text assignment and the spinner's accessible value action parse time.
/// </remarks>
/// <example><code>
/// var picker = new TimePicker { Time = new TimeOnly(14, 30), Format = "HH:mm:ss", StepSeconds = 30 };
/// picker.MinimumTime = new TimeOnly(9, 0);
/// picker.MaximumTime = new TimeOnly(18, 0);
/// picker.TimeChanged += (_, _) =&gt; Console.WriteLine(picker.Time);
/// </code></example>
[DefaultProperty(nameof(Time)), DefaultEvent(nameof(TimeChanged))]
public class TimePicker : Control
{
    private readonly TimeEditor editor;
    private string? format, cultureName;
    private CultureInfo? culture;
    private int stepSeconds = 60;
    // Notification checkpoint only. All values and nullable state are read from the spinner.
    private TimeOnly? publishedTime;

    /// <summary>Creates an empty time spinner with full-day bounds and a one-minute step.</summary>
    public TimePicker()
    {
        editor = Controls.AddImplicitControl(new TimeEditor(this));
        editor.ValueChanged += HandleValueChanged;
        editor.TextChanged += (_, _) => { if (!IsDisposed && !Disposing) base.OnTextChanged(EventArgs.Empty); };
        TabStop = false;
    }

    /// <summary>Gets or sets the selected time, or null for an unchecked empty selection.</summary>
    /// <remarks>
    /// UI thread only. Retains all 100-nanosecond ticks, including precision hidden by Format.
    /// A nonnull assignment activates the spinner, including reassignment of its retained value.
    /// Out-of-range assignments throw before changing state. A null assignment keeps its retained
    /// time for later checkbox activation and publishes at most one effective TimeChanged event.
    /// </remarks>
    [DefaultValue(null)]
    public TimeOnly? Time
    {
        get { VerifyAccess(); return editor.Checked ? TimeOnly.FromDateTime(editor.Value) : null; }
        set
        {
            VerifyAccess();
            if (value is { } time) editor.Value = editor.DateAnchor.AddTicks(time.Ticks);
            else editor.Checked = false;
        }
    }

    /// <summary>Gets or sets the inclusive minimum time; the default is midnight.</summary>
    /// <remarks>UI thread only. Must not exceed MaximumTime. Clamps a selected value; an empty selection stays empty.</remarks>
    public TimeOnly MinimumTime
    {
        get { VerifyAccess(); return TimeOnly.FromDateTime(editor.MinDate); }
        set { VerifyAccess(); editor.MinDate = editor.DateAnchor.AddTicks(value.Ticks); }
    }

    /// <summary>Gets or sets the inclusive maximum time; the default is the last tick before midnight.</summary>
    /// <remarks>UI thread only. Must not precede MinimumTime. Clamps a selected value; wrapping ranges are rejected.</remarks>
    public TimeOnly MaximumTime
    {
        get { VerifyAccess(); return TimeOnly.FromDateTime(editor.MaxDate); }
        set { VerifyAccess(); editor.MaxDate = editor.DateAnchor.AddTicks(value.Ticks); }
    }

    /// <summary>Gets or sets a TimeOnly format string; null uses the culture's short time format.</summary>
    /// <remarks>
    /// UI thread only. For example HH:mm, hh:mm tt, HH:mm:ss or HH:mm:ss.fffffff. Explicit formats
    /// also define exact text parsing; null accepts the culture's time parser. Invalid formats
    /// throw before replacement. Formatting does not round the stored time or affect bounds.
    /// </remarks>
    [DefaultValue(null)]
    public string? Format
    {
        get => format;
        set
        {
            VerifyAccess();
            _ = TimeOnly.MinValue.ToString(value ?? "t", EffectiveCulture);
            if (format == value) return;
            format = value;
            PublishRepresentationChanged();
        }
    }

    /// <summary>Gets or sets a culture name, or null to use CurrentCulture on the UI thread.</summary>
    /// <remarks>
    /// UI thread only. Empty selects the invariant culture. The cached BCL culture is read-only;
    /// no application converter or calendar code is executed. Reformatting raises TextChanged,
    /// not TimeChanged. Use Format for explicit 12/24-hour and fractional-second presentation.
    /// </remarks>
    [DefaultValue(null)]
    public string? CultureName
    {
        get => cultureName;
        set
        {
            VerifyAccess();
            CultureInfo? next = value is null ? null : CultureInfo.GetCultureInfo(value);
            _ = TimeOnly.MinValue.ToString(format ?? "t", next ?? CultureInfo.CurrentCulture);
            if (cultureName == value) return;
            cultureName = value;
            culture = next;
            PublishRepresentationChanged();
        }
    }

    /// <summary>Gets or sets the spinner step in whole seconds, from one through 86400; the default is 60.</summary>
    /// <remarks>UI thread only. Existing keyboard, wheel, pointer and accessible step actions share this increment and clamp at bounds.</remarks>
    [DefaultValue(60)]
    public int StepSeconds
    {
        get => stepSeconds;
        set
        {
            VerifyAccess();
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 86400);
            stepSeconds = value;
        }
    }

    /// <summary>Gets the formatted selected time or empty text; assignment parses a time or clears on whitespace.</summary>
    /// <remarks>UI thread only. Explicit Format uses exact time parsing. Invalid/out-of-range text leaves the existing value unchanged.</remarks>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => editor?.DisplayText ?? string.Empty;
        set { VerifyAccess(); editor.Text = value; }
    }

    /// <summary>Occurs on the UI thread after the effective nullable time changes.</summary>
    /// <remarks>State is committed before handlers run. Handlers may set a newer time/range, redirect focus or dispose; older work never restores a cached value.</remarks>
    public event EventHandler? TimeChanged;

    /// <summary>Requests focus for the actual spinner through the canonical focus scope.</summary>
    /// <returns>Whether the spinner owns focus after application validation/focus callbacks.</returns>
    public bool FocusEditor()
    {
        VerifyAccess();
        var lifetime = new AccessibilityControlLifetime(this);
        editor.Select();
        return lifetime.IsCurrent && editor.Focused;
    }

    internal DateTimePicker Editor => editor;
    private CultureInfo EffectiveCulture => culture ?? CultureInfo.CurrentCulture;

    private void VerifyAccess()
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Disposing, this);
        GetFocusScope().VerifyAccess();
    }

    private void HandleValueChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing) return;
        TimeOnly? current = Time;
        if (current == publishedTime) return;
        publishedTime = current;
        TimeChanged?.Invoke(this, e);
    }

    private void PublishRepresentationChanged()
    {
        editor.Invalidate();
        editor.NotifyAccessibilityClients(AccessibleEvents.ValueChange);
        base.OnTextChanged(EventArgs.Empty);
    }

    /// <inheritdoc/>
    protected override Size DefaultSize => new(200, 32);

    /// <inheritdoc/>
    protected override void OnLayout(LayoutEventArgs e)
    {
        if (editor is not null && !editor.IsDisposed) editor.Bounds = LogicalPaddedClientRectangle;
        base.OnLayout(e);
    }

    /// <inheritdoc/>
    protected override AccessibleObject CreateAccessibilityInstance() => new TimeAccessibleObject(this);

    private sealed class TimeAccessibleObject(TimePicker owner) : ControlAccessibleObject(owner)
    {
        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            if (Owner is TimePicker { IsDisposed: false } current) yield return current.editor.AccessibilityObject;
            foreach (var child in base.GetAccessibilityChildren()) yield return child;
        }
    }

    private sealed class TimeEditor : DateTimePicker
    {
        private readonly TimePicker? owner;
        internal DateTime DateAnchor { get; }

        internal TimeEditor(TimePicker owner)
        {
            this.owner = owner;
            var minimum = MinimumDateTime;
            DateAnchor = new(minimum.Year, minimum.Month, minimum.Day, 0, 0, 0, DateTimeKind.Unspecified);
            ShowCheckBox = true;
            ShowUpDown = true;
            base.Format = DateTimePickerFormat.Time;
            MinDate = DateAnchor;
            MaxDate = DateAnchor.AddDays(1).AddTicks(-1);
            Value = DateAnchor;
            Checked = false;
        }

        internal override bool ActivateOnRangeClamp => false;
        internal override string CheckedAccessibleName => "Enable time";
        internal override string FormatDisplayValue(DateTime candidate)
            => TimeOnly.FromDateTime(candidate).ToString(owner?.format ?? "t", owner?.EffectiveCulture ?? CultureInfo.CurrentCulture);
        internal override DateTime ParseValueText(string text)
        {
            var provider = owner?.EffectiveCulture ?? CultureInfo.CurrentCulture;
            var parsed = owner?.format is { } pattern
                ? TimeOnly.ParseExact(text, pattern, provider)
                : TimeOnly.Parse(text, provider);
            return DateAnchor.AddTicks(parsed.Ticks);
        }
        internal override void ResetValue() => Checked = false;
        internal override DateTime GetSteppedValue(int delta) => Value.AddSeconds((long)(owner?.stepSeconds ?? 60) * delta);
    }
}
