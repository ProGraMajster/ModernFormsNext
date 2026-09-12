using System.Runtime.ExceptionServices;
using ModernFormsNext.Accessibility;

namespace ModernFormsNext;

public partial class NumericUpDown
{
    private bool steppingValue;
    private long valueVersion;
    internal TextBox AccessibilityEditor => editor;

    /// <inheritdoc/>
    internal override void OnPresentationContentMetricsChanged()
    {
        base.OnPresentationContentMetricsChanged();
        UpdateEditorBounds();
    }

    // All entry points use the existing decimal value/editor, including semantic button actions.
    // A nested step from validation is rejected; an ordinary reentrant Value assignment remains
    // supported and is never overwritten by continuation after a detached/disposed callback.
    private bool IsCurrentNumericInput(AccessibilityControlLifetime lifetime)
        => lifetime.IsCurrent && !Disposing && Enabled && Visible && FindWindow()?.InputBindingsClosed != true;

    private void StepValue(bool increase)
    {
        if (steppingValue || IsDisposed || Disposing) return;
        var lifetime = new AccessibilityControlLifetime(this);
        bool wasVisible = Visible, wasEnabled = Enabled;
        steppingValue = true;
        try
        {
            ValidateEditText(false);
            if (!lifetime.IsCurrent || Disposing || FindWindow()?.InputBindingsClosed == true
                || (wasVisible && !Visible) || (wasEnabled && !Enabled)) return;
            decimal next;
            try { next = increase ? checked(currentValue + increment) : checked(currentValue - increment); }
            catch (OverflowException) { next = increase ? decimal.MaxValue : decimal.MinValue; }
            SetValue(next, true);
        }
        finally { steppingValue = false; }
    }

    private void ChangeRange(Action mutation)
    {
        var previous = (minimum, maximum, increment, decimalPlaces, allowDecimalValues, currentValue);
        Exception? failure = null;
        try { mutation(); }
        catch (Exception exception) { failure = exception; }
        try
        {
            if (previous != (minimum, maximum, increment, decimalPlaces, allowDecimalValues, currentValue)
                && !IsDisposed && !Disposing)
                NotifyAccessibilityClients(AccessibleEvents.RangeValueChanged);
        }
        catch (Exception exception) { failure = CombineFailure(failure, exception); }
        ThrowFailure(failure);
    }

    private void RunWithAccessibilityNotification(Action callback, AccessibleEvents kind)
    {
        Exception? failure = null;
        try { callback(); }
        catch (Exception exception) { failure = exception; }
        try { if (!IsDisposed && !Disposing) NotifyAccessibilityClients(kind); }
        catch (Exception exception) { failure = CombineFailure(failure, exception); }
        ThrowFailure(failure);
    }

    private static Exception CombineFailure(Exception? first, Exception next)
        => first is null ? next : new AggregateException(first, next);
    private static void ThrowFailure(Exception? failure)
    {
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
