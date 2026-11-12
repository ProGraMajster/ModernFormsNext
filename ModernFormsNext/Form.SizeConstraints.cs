using ModernFormsNext.WindowKit;

namespace ModernFormsNext;

public partial class Form
{
    private void SetSizeConstraint(System.Drawing.Size value, bool minimum)
    {
        if ((minimum ? minimum_size : maximum_size) == value) return;
        var nextMinimum = minimum ? value : minimum_size;
        var nextMaximum = minimum ? maximum_size : value;
        if (!nextMinimum.IsEmpty && !nextMaximum.IsEmpty) {
            if (minimum)
                nextMaximum = new(Math.Max(nextMinimum.Width, nextMaximum.Width), Math.Max(nextMinimum.Height, nextMaximum.Height));
            else
                nextMinimum = new(Math.Min(nextMinimum.Width, nextMaximum.Width), Math.Min(nextMinimum.Height, nextMaximum.Height));
        }
        bool pairedChanged = minimum ? maximum_size != nextMaximum : minimum_size != nextMinimum;

        // A rejecting backend must leave the managed pair and notification generation untouched.
        Window.SetMinMaxSize(nextMinimum.ToAvaloniaSize(), nextMaximum.ToAvaloniaSize());
        minimum_size = nextMinimum;
        maximum_size = nextMaximum;
        long version = AdvanceSizeConstraintVersion();
        List<Exception>? failures = null;
        try {
            // Preserve the existing clamp semantics. Resize/layout can run user code and replace
            // these limits or close the form before constraint notifications are published.
            var size = Size;
            if (minimum && (size.Width < value.Width || size.Height < value.Height))
                Size = new(Math.Max(size.Width, value.Width), Math.Max(size.Height, value.Height));
            else if (!minimum && !value.IsEmpty && (size.Width > value.Width || size.Height > value.Height))
                Size = new(Math.Min(size.Width, value.Width), Math.Min(size.Height, value.Height));
        }
        catch (Exception failure) { (failures ??= []).Add(failure); }

        NotifyConstraint(minimum, version, ref failures);
        if (pairedChanged) NotifyConstraint(!minimum, version, ref failures);
        ThrowSizeConstraintFailures(failures);
    }

    private void NotifyConstraint(bool minimum, long version, ref List<Exception>? failures)
    {
        if (!IsCurrentSizeConstraintVersion(version)) return;
        try { RaiseSizeConstraintNotification(minimum, version); }
        catch (Exception failure) { (failures ??= []).Add(failure); }
    }
}
