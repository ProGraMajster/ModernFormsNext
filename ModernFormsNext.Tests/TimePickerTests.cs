using System.Globalization;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class TimePickerTests
{
    [Fact]
    public void ExistingDatePickerExplicitEqualValueActivatesUncheckedSelection()
    {
        using var picker = new DateTimePicker { ShowCheckBox = true, Value = new(2026, 10, 17, 12, 34, 56) };
        picker.Checked = false;
        int changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        picker.Value = picker.Value;
        Assert.True(picker.Checked);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void EmptyStateRetainsOneCanonicalValueAndAllTimeTicks()
    {
        using var picker = new TimePicker();
        Assert.Null(picker.Time);
        Assert.Empty(picker.Text);
        Assert.Equal(TimeOnly.MinValue, picker.MinimumTime);
        Assert.Equal(TimeOnly.MaxValue, picker.MaximumTime);
        int changes = 0;
        picker.TimeChanged += (_, _) => changes++;
        var exact = new TimeOnly(123_456_789_012L);
        picker.Time = exact;
        picker.Time = exact;
        Assert.Equal(exact, picker.Time);
        Assert.Equal(exact.Ticks, picker.Editor.Value.TimeOfDay.Ticks);
        Assert.Equal(DateTimeKind.Unspecified, picker.Editor.Value.Kind);
        Assert.Equal(1, changes);
        picker.Time = null;
        picker.Time = null;
        Assert.Equal(2, changes);
        Assert.Equal(exact.Ticks, picker.Editor.Value.TimeOfDay.Ticks);
        picker.Time = exact;
        Assert.Equal(exact, picker.Time);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void RangeChangesPreserveEmptyStateAndClampSelectedState()
    {
        using var picker = new TimePicker();
        int changes = 0;
        picker.TimeChanged += (_, _) => changes++;
        picker.MinimumTime = new(9, 0);
        picker.MaximumTime = new(17, 0);
        Assert.Null(picker.Time);
        Assert.Equal(0, changes);
        picker.Editor.Checked = true;
        Assert.Equal(new TimeOnly(9, 0), picker.Time);
        picker.MinimumTime = new(10, 0);
        Assert.Equal(new TimeOnly(10, 0), picker.Time);
        picker.Time = new(16, 0);
        picker.MaximumTime = new(15, 0);
        Assert.Equal(new TimeOnly(15, 0), picker.Time);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.MinimumTime = new(18, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.MaximumTime = new(8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.Time = new(20, 0));
        Assert.Equal(new TimeOnly(15, 0), picker.Time);
    }

    [Fact]
    public void LegacyDatePickerRangeClampStillActivatesItsUncheckedValue()
    {
        using var picker = new DateTimePicker { ShowCheckBox = true, Value = new(2026, 1, 1) };
        picker.Checked = false;
        picker.MinDate = new(2026, 2, 1);
        Assert.True(picker.Checked);
        Assert.Equal(picker.MinDate, picker.Value);
    }

    [Theory]
    [InlineData("en-US", null)]
    [InlineData("en-GB", null)]
    [InlineData("pl-PL", "HH:mm")]
    [InlineData("en-US", "hh:mm:ss tt")]
    [InlineData("ar-SA", "HH:mm:ss.fffffff")]
    [InlineData("ja-JP", "HH:mm:ss")]
    public void CultureFormatAndTextUseTimeOnlyWithoutChangingHiddenPrecision(string culture, string? format)
    {
        var exact = new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(3_456_789));
        using var picker = new TimePicker { CultureName = culture, Format = format, Time = exact };
        var provider = CultureInfo.GetCultureInfo(culture);
        Assert.Equal(exact.ToString(format ?? "t", provider), picker.Text);
        Assert.Equal(exact, picker.Time);
        string text = new TimeOnly(9, 23, 24).ToString(format ?? "t", provider);
        picker.Text = text;
        var expected = format is null ? TimeOnly.Parse(text, provider) : TimeOnly.ParseExact(text, format, provider);
        Assert.Equal(expected, picker.Time);
        Assert.Throws<FormatException>(() => picker.Text = "not a time");
        Assert.Equal(expected, picker.Time);
        picker.Text = " ";
        Assert.Null(picker.Time);
        Assert.Empty(picker.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void InvalidStepIsRejectedBeforeReplacement(int seconds)
    {
        using var picker = new TimePicker();
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.StepSeconds = seconds);
        Assert.Equal(60, picker.StepSeconds);
    }

    [Fact]
    public void InvalidFormatAndCultureLeaveThePreviousRepresentation()
    {
        using var picker = new TimePicker { CultureName = "en-GB", Format = "HH:mm", Time = new(12, 30) };
        Assert.Throws<FormatException>(() => picker.Format = "yyyy-MM-dd");
        Assert.Equal("HH:mm", picker.Format);
        Assert.Equal("12:30", picker.Text);
        Assert.Throws<CultureNotFoundException>(() => picker.CultureName = "not_a_culture!");
        Assert.Equal("en-GB", picker.CultureName);
    }

    [Fact]
    public void AccessibleSpinnerIsTheActualCanonicalPeerAndClampsWithoutMidnightWrap()
    {
        using var picker = new TimePicker { Format = "HH:mm:ss.fffffff", Time = TimeOnly.MaxValue, StepSeconds = 1 };
        using var surface = new SkiaControlSurface(picker);
        surface.Resize(260, 40);
        var spinner = picker.AccessibilityObject.GetChild(0)!;
        Assert.Same(picker.Editor.AccessibilityObject, spinner);
        Assert.Equal(AccessibleControlType.Spinner, spinner.ControlType);
        Assert.Equal("Enable time", spinner.GetChild(0)!.Name);
        Assert.False(spinner.PerformAction(AccessibleActions.Increment));
        Assert.Equal(TimeOnly.MaxValue, picker.Time);
        Assert.True(spinner.PerformAction(AccessibleActions.Decrement));
        Assert.Equal(new TimeOnly(TimeOnly.MaxValue.Ticks - TimeSpan.TicksPerSecond), picker.Time);
        Assert.True(spinner.PerformAction(AccessibleActions.SetValue, "00:00:00.0000000"));
        Assert.False(spinner.PerformAction(AccessibleActions.Decrement));
        Assert.True(picker.FocusEditor());
        Assert.True(picker.Editor.Focused);
    }

    [Theory]
    [InlineData("new-value")]
    [InlineData("clear")]
    [InlineData("range")]
    [InlineData("dispose")]
    public void TimeChangedCannotRestoreAnOlderState(string change)
    {
        using var picker = new TimePicker();
        bool first = true;
        picker.TimeChanged += (_, _) =>
        {
            if (!first) return;
            first = false;
            switch (change)
            {
                case "new-value": picker.Time = new(13, 0); break;
                case "clear": picker.Time = null; break;
                case "range": picker.MinimumTime = new(14, 0); break;
                case "dispose": picker.Dispose(); break;
            }
        };
        picker.Time = new(12, 0);
        if (change == "dispose") Assert.True(picker.IsDisposed);
        else Assert.Equal(change == "clear" ? (TimeOnly?)null : new TimeOnly(change == "range" ? 14 : 13, 0), picker.Time);
    }

    [Fact]
    public void RepresentationChangeDoesNotPublishAValueChangeOrOverwriteNewerFormatting()
    {
        using var picker = new TimePicker { Time = new(13, 14, 15) };
        int values = 0;
        picker.TimeChanged += (_, _) => values++;
        bool first = true;
        picker.TextChanged += (_, _) => { if (first) { first = false; picker.Format = "HH:mm:ss"; } };
        picker.Format = "HH:mm";
        Assert.Equal("HH:mm:ss", picker.Format);
        Assert.Equal("13:14:15", picker.Text);
        Assert.Equal(0, values);
    }

}
