using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class DateTimePicker
{
    private bool retiringDatePicker;
    internal bool IsCurrentCalendar(DateTimePickerCalendar calendar) => ReferenceEquals(popupCalendar, calendar) && isDroppedDown;
    private bool CanUseDatePicker => !retiringDatePicker && !IsDisposed && !Disposing && Enabled && Visible
        && FindWindow()?.InputBindingsClosed != true;

    private bool CanOpenDropDown => CanUseDatePicker && Checked && !ShowUpDown
        && FindForm() is { InputBindingsClosed: false };

    private bool OpenDropDown()
    {
        if (!CanOpenDropDown) return false;
        if (isDroppedDown) return true;
        var lifetime = new AccessibilityControlLifetime(this);
        var hostForm = FindForm()!;
        var popup = new CalendarPopup(hostForm) { Size = new System.Drawing.Size(232, 268) };
        try
        {
            var calendar = new DateTimePickerCalendar(this)
            {
                Dock = DockStyle.Fill, Value = Value, MinDate = MinDate, MaxDate = MaxDate
            };
            popup.Controls.Add(calendar);
            if (!lifetime.IsCurrent || !CanOpenDropDown) { popup.Dispose(); return false; }
            popupWindow = popup;
            popupCalendar = calendar;
            isDroppedDown = true;
            popup.Closed += PopupClosed;
            OnDropDown(EventArgs.Empty);
            // DropDown callbacks may close/dispose/reparent this control or open a replacement.
            // The captured popup alone owns this continuation; never read a replacement field.
            if (!ReferenceEquals(popupWindow, popup)) return false;
            if (!lifetime.IsCurrent || !CanOpenDropDown) { CloseDropDown(); return false; }
            Invalidate(buttonRect);
            popup.Show(this, 0, Height);
            if (!ReferenceEquals(popupWindow, popup)) return false;
            if (!lifetime.IsCurrent || !CanOpenDropDown || !popup.Visible)
            { CloseDropDown(); return false; }
            calendar.Select();
            return ReferenceEquals(popupWindow, popup) && lifetime.IsCurrent;
        }
        catch (Exception failure)
        {
            try
            {
                if (ReferenceEquals(popupWindow, popup)) CloseDropDown();
                else if (!popup.InputBindingsClosed) popup.Dispose();
            }
            catch (Exception cleanup)
            { throw new AggregateException("Calendar popup opening and cleanup failed.", failure, cleanup); }
            throw;
        }
    }

    private void PopupClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, popupWindow)) CloseDropDown();
    }

    private void RetireDropDown()
    {
        var popup = popupWindow;
        bool notify = isDroppedDown;
        // Retire ownership before any native, visibility, input or CloseUp observer runs.
        // A callback may open another popup; its fields must survive this cleanup.
        popupWindow = null;
        popupCalendar = null;
        isDroppedDown = false;
        List<Exception>? failures = null;
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception failure) { (failures ??= []).Add(failure); }
        }
        if (popup is not null)
        {
            popup.Closed -= PopupClosed;
            if (!popup.InputBindingsClosed && popup.Visible) Attempt(popup.Hide);
            Attempt(popup.Dispose);
        }
        if (notify)
        {
            Attempt(() => OnCloseUp(EventArgs.Empty));
            Attempt(() => Invalidate(buttonRect));
        }
        if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures is not null) throw new AggregateException("Calendar popup cleanup failed.", failures);
    }

    /// <inheritdoc/>
    protected override void OnParentChanged(EventArgs e)
    {
        try { CloseDropDown(); }
        finally { base.OnParentChanged(e); }
    }

    /// <inheritdoc/>
    protected override void OnEnabledChanged(EventArgs e)
    {
        try { if (!Enabled) CloseDropDown(); }
        finally { base.OnEnabledChanged(e); }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        retiringDatePicker = true;
        Exception? failure = null;
        try { CloseDropDown(); }
        catch (Exception error) { failure = error; }
        // Closing the owned popup and disposing this control are independent cleanup steps.
        // Preserve both observer failures instead of allowing a finally exception to mask one.
        try { base.Dispose(true); }
        catch (Exception cleanup) when (failure is not null)
        { throw new AggregateException("Date picker popup and control disposal failed.", failure, cleanup); }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    // This picker owns a fresh popup and its entire tree for each open session. WindowBase's
    // Component disposal alone does not destroy its backend or dispose caller-owned controls.
    // Keep this stronger ownership local to the calendar, without changing reusable popups.
    private sealed class CalendarPopup(Form parent) : PopupWindow(parent)
    {
        private bool retired;

        protected override void Dispose(bool disposing)
        {
            if (!disposing) { base.Dispose(false); return; }
            if (retired) return;
            retired = true;
            List<Exception>? failures = null;
            void Attempt(Action cleanup)
            {
                try { cleanup(); }
                catch (Exception failure) { (failures ??= []).Add(failure); }
            }
            // Backend destruction delivers the normal Closed/input/provider teardown. Retire
            // before that callback, as it may synchronously dispose this same captured popup.
            Attempt(window.Dispose);
            Attempt(adapter.Dispose);
            Attempt(() => base.Dispose(true));
            if (failures is { Count: 1 }) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures is not null) throw new AggregateException("Calendar window and owned controls could not be fully disposed.", failures);
        }
    }
}
