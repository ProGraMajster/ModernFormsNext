using System.Drawing;
using System.Globalization;
using System.Reflection;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

[Collection(AccessibilitySemanticCollection.Name)]
public sealed class DateTimePickerAccessibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyTheCheckboxPartInheritsTheCheckedDateState(bool upDown)
    {
        using var picker = new TestPicker { ShowCheckBox = true, Checked = true, ShowUpDown = upDown };
        var root = picker.AccessibilityObject;
        Assert.True(root.State.HasFlag(AccessibleStates.Checked));
        Assert.True(root.GetChild(0)!.State.HasFlag(AccessibleStates.Checked));
        for (int index = 1; index < root.GetChildCount(); ++index)
            Assert.Equal(AccessibleStates.None, root.GetChild(index)!.State & (AccessibleStates.Checked | AccessibleStates.Mixed));
    }

    [Fact]
    public void UncheckedPickerRetainsAccessibleCheckboxAndKeyboardSpacePath()
    {
        using var picker = new TestPicker { ShowCheckBox = true, Checked = false };
        var root = picker.AccessibilityObject;
        Assert.False(root.State.HasFlag(AccessibleStates.Unavailable));
        Assert.False(root.State.HasFlag(AccessibleStates.Checked));
        var check = root.GetChild(0)!;
        Assert.Equal(AccessibleControlType.CheckBox, check.ControlType);
        Assert.True(check.PerformAction(AccessibleActions.Toggle));
        Assert.True(picker.Checked);
        Assert.True(root.State.HasFlag(AccessibleStates.Checked));
        picker.Checked = false;
        var key = picker.Key(Keys.Space);
        Assert.True(key.Handled);
        Assert.True(picker.Checked);
        picker.Enabled = false;
        Assert.False(check.PerformAction(AccessibleActions.Toggle));
        Assert.False(picker.Key(Keys.Space).Handled);
    }

    [Fact]
    public void WindowlessPickerHasValueAndStepsWithoutAdvertisingPopup()
    {
        using var picker = new TestPicker { Format = DateTimePickerFormat.Short, Value = new(2026, 9, 11) };
        var root = picker.AccessibilityObject;
        Assert.False(root.SupportedActions.HasFlag(AccessibleActions.Expand));
        Assert.False(root.State.HasFlag(AccessibleStates.HasPopup));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.True(root.PerformAction(AccessibleActions.SetValue, "12.09.2026"));
            Assert.Equal(new DateTime(2026, 9, 12), picker.Value);
            Assert.True(root.PerformAction(AccessibleActions.Increment));
            Assert.Equal(new DateTime(2026, 9, 13), picker.Value);
            Assert.Throws<FormatException>(() => root.PerformAction(AccessibleActions.SetValue, "invalid date"));
            Assert.False(root.PerformAction(AccessibleActions.SetValue, 42));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Theory]
    [InlineData("yyyy")]
    [InlineData("MM")]
    [InlineData("dd")]
    [InlineData("HH:mm")]
    public void StepsClampBeforeCalendarArithmeticCanOverflow(string format)
    {
        using var picker = new TestPicker { ShowUpDown = true, Format = DateTimePickerFormat.Custom, CustomFormat = format };
        picker.Value = picker.MaxDate;
        picker.Key(Keys.Up);
        Assert.Equal(picker.MaxDate, picker.Value);
        picker.Value = picker.MinDate;
        picker.Key(Keys.Down);
        Assert.Equal(picker.MinDate, picker.Value);
    }

    [Fact]
    public void CalendarExposesRealDateGridHeadersAndRetiresOldPagePeers()
    {
        using var fixture = new PopupFixture();
        var calendar = fixture.Open();
        var root = calendar.AccessibilityObject;
        var grid = root.GridProvider!;
        Assert.Equal(6, grid.RowCount);
        Assert.Equal(7, grid.ColumnCount);
        Assert.Equal(7, grid.GetColumnHeaders().Count);
        Assert.Equal(54, root.GetChildCount());
        var selected = root.GetSelected()!;
        Assert.Equal(fixture.Picker.Value.ToString("D", CultureInfo.CurrentCulture), selected.Name);
        var cell = grid.GetItem(2, 3)!;
        Assert.Same(cell, root.GetChild(12 + 2 * 7 + 3));
        Assert.Equal(2, cell.GridCell!.Row);
        Assert.Same(grid.GetColumnHeaders()[3], Assert.Single(cell.GridCell.ColumnHeaders));
        Assert.True(root.GetChild(1)!.PerformAction(AccessibleActions.Invoke));
        Assert.Null(cell.Parent);
        Assert.False(cell.PerformAction(AccessibleActions.Select));
        Assert.NotEqual(cell.RuntimeId, grid.GetItem(2, 3)!.RuntimeId);
        Assert.True(root.GetChild(2)!.PerformAction(AccessibleActions.Invoke));
        Assert.Equal(3, grid.RowCount);
        Assert.Equal(4, grid.ColumnCount);
        Assert.Empty(grid.GetColumnHeaders());
        Assert.Equal(16, root.GetChildCount());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalendarPopupInheritsCurrentOwnerProtectionAcrossItsSeparateNativeRoot(bool initiallyProtected)
    {
        using var fixture = new PopupFixture();
        fixture.Form.Controls.Remove(fixture.Picker);
        var parent = fixture.Form.Controls.Add(new SensitivePanel { Size = new(300, 100), Sensitive = initiallyProtected });
        parent.Controls.Add(fixture.Picker);
        var calendar = fixture.Open();
        var popup = fixture.Picker.PopupWindow!;
        var root = calendar.AccessibilityObject;
        var native = Assert.IsType<PlatformAccessibleObjectAdapter>(PlatformAccessibleObjectAdapter.From(root));

        // Privacy crosses the logical owner's boundary; the actual popup hierarchy stays
        // independent, with its real adapter as parent rather than a synthetic picker parent.
        Assert.Same(popup.adapter.AccessibilityObject, root.Parent);
        Assert.Equal(initiallyProtected, root.IsSensitive);
        if (initiallyProtected)
        {
            Assert.Null(root.Value);
            Assert.Null(root.GridProvider);
            Assert.Null(ReadNativeGridCapability(native));
        }

        parent.Sensitive = false;
        var provider = Assert.IsAssignableFrom<AccessibleGridProvider>(root.GridProvider);
        var selected = Assert.IsAssignableFrom<AccessibleObject>(root.GetSelected());
        Assert.Equal(6, provider.RowCount);
        Assert.Equal(7, provider.ColumnCount);
        Assert.NotNull(root.Value);
        Assert.NotNull(ReadNativeGridCapability(native));

        // Keep both canonical and platform providers while protection changes after Show.
        // Reading a retained capability must not disclose the previously selected date.
        parent.Sensitive = true;
        Assert.True(root.IsSensitive);
        Assert.True(native.IsSensitive);
        Assert.Null(root.Value);
        Assert.Null(root.GetSelected());
        Assert.Null(selected.GridCell);
        Assert.Null(root.GridProvider);
        Assert.Null(ReadNativeGridCapability(native));
        Assert.Equal(0, provider.RowCount);
        Assert.Equal(0, provider.ColumnCount);
        Assert.Null(provider.GetItem(0, 0));
        Assert.Empty(provider.GetColumnHeaders());

        // Sensitivity suppresses payload capabilities, not ordinary calendar navigation.
        Assert.True(selected.PerformAction(AccessibleActions.Focus));
        Assert.True(root.GetChild(1)!.PerformAction(AccessibleActions.Invoke));
        Assert.Same(popup, fixture.Picker.PopupWindow);
        parent.Sensitive = false;
        Assert.False(root.IsSensitive);
        Assert.NotNull(root.Value);
        Assert.Same(provider, root.GridProvider);
        Assert.Equal(6, provider.RowCount);
        Assert.NotNull(ReadNativeGridCapability(native));

        fixture.Picker.CloseDropDown();
        Assert.Null(root.Value);
        Assert.Null(root.GridProvider);
        Assert.Equal(0, provider.RowCount);
        Assert.Null(provider.GetItem(0, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalendarPrivacyCallbackFailureOrSessionRetirementFailsClosed(bool retirePopup)
    {
        using var fixture = new PopupFixture();
        fixture.Form.Controls.Remove(fixture.Picker);
        var parent = fixture.Form.Controls.Add(new SensitivePanel { Size = new(300, 100) });
        parent.Controls.Add(fixture.Picker);
        var root = fixture.Open().AccessibilityObject;
        var provider = root.GridProvider!;
        var selected = root.GetSelected()!;
        parent.ReadSensitivity = () =>
        {
            if (!retirePopup) throw new InvalidOperationException("privacy callback");
            parent.ReadSensitivity = null;
            fixture.Picker.CloseDropDown();
        };

        Assert.True(root.IsSensitive);
        Assert.Null(root.Value);
        Assert.Null(root.GetSelected());
        Assert.Null(root.GridProvider);
        Assert.Equal(0, provider.RowCount);
        Assert.Null(provider.GetItem(0, 0));
        Assert.Null(selected.GridCell);
        if (retirePopup)
        {
            Assert.Null(fixture.Picker.PopupWindow);
            Assert.True(selected.State.HasFlag(AccessibleStates.Unavailable));
        }
        parent.ReadSensitivity = null;
    }

    [Fact]
    public void CalendarPopupDoesNotPublishItsProtectedOwnerLabelAsAnIndependentNativeName()
    {
        using var fixture = new PopupFixture();
        fixture.Form.Controls.Remove(fixture.Picker);
        var parent = fixture.Form.Controls.Add(new SensitivePanel { Size = new(300, 100), Sensitive = true });
        parent.Controls.Add(fixture.Picker);
        const string privateLabel = "private-calendar-owner-label";
        fixture.Picker.AccessibleName = privateLabel;
        var root = fixture.Open().AccessibilityObject;
        var native = PlatformAccessibleObjectAdapter.From(root)!;

        // Native adapters intentionally retain an element's own authored label. The
        // calendar must therefore redact a protected owner's inherited label itself.
        Assert.True(root.IsSensitive);
        Assert.Null(root.Name);
        Assert.Null(native.Name);

        parent.Sensitive = false;
        Assert.Equal(privateLabel, root.Name);
        Assert.Equal(privateLabel, native.Name);
        fixture.Picker.AccessibleName = null;
        Assert.Equal("Calendar", root.Name);
        root.Name = "Calendar navigation";
        Assert.Equal("Calendar navigation", root.Name);
        parent.Sensitive = true;
        Assert.Null(root.Name);
        Assert.Null(native.Name);
        fixture.Picker.CloseDropDown();
        Assert.Null(root.Name);
        Assert.Null(native.Name);
    }

    // Match the existing text-capability probe: Core.Tests has no WindowKit friend access,
    // so inspect the real optional adapter without broadening production visibility.
    private static object? ReadNativeGridCapability(PlatformAccessibleObjectAdapter node)
        => node.GetType().GetProperty("GridInfo")!.GetValue(node);

    [Fact]
    public void CalendarSelectionPreservesTimeTicksKindAndClampsBoundaryTime()
    {
        using var fixture = new PopupFixture();
        fixture.Picker.Value = DateTime.SpecifyKind(new DateTime(2026, 9, 11, 12, 30, 5).AddTicks(1234), DateTimeKind.Utc);
        var calendar = fixture.Open();
        var date = new DateTime(2026, 9, 13);
        int index = (date - calendar.GetFirstVisibleDate()).Days;
        Assert.True(calendar.AccessibilityObject.GridProvider!.GetItem(index / 7, index % 7)!.PerformAction(AccessibleActions.Select));
        Assert.Equal(date.AddTicks(new TimeSpan(0, 12, 30, 5).Ticks + 1234), fixture.Picker.Value);
        Assert.Equal(DateTimeKind.Utc, fixture.Picker.Value.Kind);
        Assert.Null(fixture.Picker.PopupWindow);
        fixture.Picker.MinDate = new DateTime(2026, 9, 13, 16, 0, 0);
        fixture.Open();
        fixture.Picker.ApplyDropDownValue(new DateTime(2026, 9, 13));
        Assert.Equal(fixture.Picker.MinDate, fixture.Picker.Value);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("dispose")]
    [InlineData("remove")]
    [InlineData("hide")]
    [InlineData("throw")]
    public void DropDownObserverCannotResumeObsoletePopup(string operation)
    {
        using var fixture = new PopupFixture();
        PopupWindow? captured = null;
        fixture.Picker.DropDown += (_, _) =>
        {
            captured = fixture.Picker.PopupWindow;
            switch (operation)
            {
                case "close": fixture.Picker.CloseDropDown(); break;
                case "dispose": fixture.Picker.Dispose(); break;
                case "remove": fixture.Form.Controls.Remove(fixture.Picker); break;
                case "hide": fixture.Picker.Visible = false; break;
                case "throw": throw new InvalidOperationException("drop-down observer");
            }
        };
        if (operation == "throw") Assert.Throws<InvalidOperationException>(() => fixture.Picker.AccessibilityObject.PerformAction(AccessibleActions.Expand));
        else Assert.False(fixture.Picker.AccessibilityObject.PerformAction(AccessibleActions.Expand));
        Assert.NotNull(captured);
        Assert.True(captured!.InputBindingsClosed);
        Assert.Null(fixture.Picker.PopupWindow);
        Assert.False(fixture.Picker.IsDropDownOpen);
    }

    [Fact]
    public void CloseUpMayOpenReplacementWithoutOldCleanupClearingIt()
    {
        using var fixture = new PopupFixture();
        fixture.Open();
        var original = fixture.Picker.PopupWindow!;
        bool reopen = true;
        fixture.Picker.CloseUp += (_, _) => { if (reopen) { reopen = false; fixture.Picker.AccessibilityObject.PerformAction(AccessibleActions.Expand); } };
        fixture.Picker.CloseDropDown();
        Assert.True(original.InputBindingsClosed);
        Assert.True(fixture.Picker.IsDropDownOpen);
        Assert.NotSame(original, fixture.Picker.PopupWindow);
        Assert.True(fixture.Picker.PopupWindow!.Visible);
    }

    [Fact]
    public void ThrowingValueAndCloseObserversStillRetirePopupAndPreserveFailures()
    {
        using var fixture = new PopupFixture();
        fixture.Open();
        var popup = fixture.Picker.PopupWindow!;
        fixture.Picker.ValueChanged += (_, _) => throw new InvalidOperationException("value");
        fixture.Picker.CloseUp += (_, _) => throw new ArgumentException("close");
        var error = Assert.Throws<AggregateException>(() => fixture.Picker.ApplyDropDownValue(new DateTime(2026, 9, 12)));
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.True(popup.InputBindingsClosed);
        Assert.Null(fixture.Picker.PopupWindow);
    }

    [Fact]
    public void NativePopupHideRetiresPickerStateAndCalendarPeers()
    {
        using var fixture = new PopupFixture();
        var calendar = fixture.Open();
        var peer = calendar.AccessibilityObject.GetSelected()!;
        fixture.Picker.PopupWindow!.Hide();
        Assert.Null(fixture.Picker.PopupWindow);
        Assert.False(fixture.Picker.IsDropDownOpen);
        Assert.False(peer.PerformAction(AccessibleActions.Select));
    }

    [Fact]
    public void HidingAnAncestorRetiresPopupThroughNormalVisibilityPropagation()
    {
        using var fixture = new PopupFixture();
        fixture.Form.Controls.Remove(fixture.Picker);
        var panel = fixture.Form.Controls.Add(new Panel { Size = new(300, 100) });
        panel.Controls.Add(fixture.Picker);
        fixture.Open();
        panel.Visible = false;
        Assert.Null(fixture.Picker.PopupWindow);
        Assert.False(fixture.Picker.IsDropDownOpen);
    }

    [Fact]
    public void DisposeClosesPopupBeforeCallbacksCanReopenIt()
    {
        using var fixture = new PopupFixture();
        fixture.Open();
        bool reopened = false;
        fixture.Picker.CloseUp += (_, _) => reopened = fixture.Picker.AccessibilityObject.PerformAction(AccessibleActions.Expand);
        fixture.Picker.Dispose();
        Assert.False(reopened);
        Assert.Null(fixture.Picker.PopupWindow);
    }

    [Fact]
    public void DisposePreservesPopupCloseAndControlDisposedFailuresAfterReleasingBothOwners()
    {
        using var fixture = new PopupFixture();
        var calendar = fixture.Open();
        var popup = fixture.Picker.PopupWindow!;
        var closeFailure = new InvalidOperationException("close observer");
        var disposeFailure = new ArgumentException("disposed observer");
        EventHandler close = (_, _) => throw closeFailure;
        EventHandler disposed = (_, _) => throw disposeFailure;
        fixture.Picker.CloseUp += close;
        fixture.Picker.Disposed += disposed;
        try
        {
            var failures = Assert.Throws<AggregateException>(fixture.Picker.Dispose).Flatten().InnerExceptions;
            Assert.Equal(2, failures.Count);
            Assert.Same(closeFailure, failures[0]);
            Assert.Same(disposeFailure, failures[1]);
            Assert.True(calendar.IsDisposed);
            Assert.True(popup.adapter.IsDisposed);
            Assert.Equal(1, ((WindowProxy)popup.window).DisposeCalls);
            Assert.True(fixture.Picker.IsDisposed);
            Assert.Null(fixture.Picker.PopupWindow);
        }
        finally
        {
            fixture.Picker.CloseUp -= close;
            fixture.Picker.Disposed -= disposed;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CultureBoundaryCalendarNamesRemainReadableForUnavailableDates(bool maximum)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            using var fixture = new PopupFixture();
            fixture.Picker.Value = maximum ? fixture.Picker.MaxDate : fixture.Picker.MinDate;
            var calendar = fixture.Open();
            var root = calendar.AccessibilityObject;
            var provider = root.GridProvider!;
            var selected = root.GetSelected()!;
            Assert.Equal(fixture.Picker.Value.ToString("D", CultureInfo.CurrentCulture), selected.Name);

            // Edge months include real adjacent dates beyond UmAlQuraCalendar's supported
            // interval. They remain labelled, unavailable cells rather than throwing getters.
            int outside = 0;
            for (int index = 0; index < 42; index++)
            {
                var date = calendar.GetFirstVisibleDate().AddDays(index);
                var cell = provider.GetItem(index / 7, index % 7)!;
                Assert.False(string.IsNullOrEmpty(cell.Name));
                if (date < fixture.Picker.MinDate.Date || date > fixture.Picker.MaxDate.Date)
                {
                    outside++;
                    Assert.True(cell.State.HasFlag(AccessibleStates.Unavailable));
                    Assert.Equal(AccessibleActions.None, cell.SupportedActions);
                }
            }
            Assert.True(outside > 0);
            Assert.True(root.GetChild(2)!.PerformAction(AccessibleActions.Invoke));
            for (int index = 0; index < 12; index++)
                Assert.False(string.IsNullOrEmpty(provider.GetItem(index / 4, index % 4)!.Name));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void RetainedCalendarProviderBecomesEmptyWhenPopupCloses()
    {
        using var fixture = new PopupFixture();
        var calendar = fixture.Open();
        var root = calendar.AccessibilityObject;
        var provider = root.GridProvider!;
        var header = provider.GetColumnHeaders()[0];
        var popup = fixture.Picker.PopupWindow!;
        fixture.Picker.CloseDropDown();
        Assert.True(calendar.IsDisposed);
        Assert.True(popup.adapter.IsDisposed);
        Assert.Equal(1, ((WindowProxy)popup.window).DisposeCalls);
        Assert.False(fixture.Picker.IsDropDownOpen);
        Assert.Null(fixture.Picker.PopupWindow);
        Assert.Null(root.GridProvider);
        Assert.Equal(0, provider.RowCount);
        Assert.Equal(0, provider.ColumnCount);
        Assert.False(provider.IsTable);
        Assert.Empty(provider.GetColumnHeaders());
        Assert.Empty(provider.GetRowHeaders());
        Assert.Null(provider.GetItem(0, 0));
        Assert.Null(header.Parent);
        Assert.Null(header.Name);
    }

    private sealed class TestPicker : DateTimePicker
    {
        public override bool Visible { get => true; set => base.Visible = value; }
        internal KeyEventArgs Key(Keys key) { var args = new KeyEventArgs(key); OnKeyDown(args); return args; }
    }

    private sealed class SensitivePanel : Panel
    {
        internal bool Sensitive;
        internal Action? ReadSensitivity;
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(SensitivePanel owner) : ControlAccessibleObject(owner)
        {
            public override bool IsSensitive { get { owner.ReadSensitivity?.Invoke(); return owner.Sensitive; } }
        }
    }

    private sealed class PopupFixture : IDisposable
    {
        internal Form Form { get; } = new(DispatchProxy.Create<IWindowImpl, WindowProxy>()) { StartPosition = FormStartPosition.Manual };
        internal DateTimePicker Picker { get; }
        internal PopupFixture()
        {
            Form.Show();
            Picker = Form.Controls.Add(new DateTimePicker { Value = new(2026, 9, 11), Size = new Size(200, 32) });
        }
        internal DateTimePickerCalendar Open()
        {
            Assert.True(Picker.AccessibilityObject.PerformAction(AccessibleActions.Expand));
            return Assert.IsType<DateTimePickerCalendar>(Picker.PopupWindow!.Controls[0]);
        }
        public void Dispose() { Picker.Dispose(); Form.Dispose(); Form.adapter.Dispose(); }
    }

    private class WindowProxy : DispatchProxy
    {
        internal int DisposeCalls;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IDisposable.Dispose)) DisposeCalls++;
            if (method?.Name == nameof(ITopLevelImpl.CreatePopup)) return DispatchProxy.Create<IPopupImpl, WindowProxy>();
            if (method?.Name is "get_RenderScaling" or "get_DesktopScaling") return 1d;
            return method?.ReturnType is { } type && type != typeof(void) && type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
