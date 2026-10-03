namespace ModernFormsNext;

/// <summary>Describes a confirmed change in the logical-to-device rendering scale.</summary>
/// <remarks>
/// Immutable and platform-neutral. Scale 1 represents 96 DPI. The scale values retain backend
/// precision; integer DPI uses the same truncation as Control.DeviceDpi. This notification cannot
/// cancel the change and contains no monitor, native handle or suggested window rectangle.
/// </remarks>
public sealed class DpiChangedEventArgs : EventArgs
{
    /// <summary>Creates data for a transition between two positive, finite rendering scales.</summary>
    /// <param name="oldScale">The previously confirmed logical-to-device scale.</param>
    /// <param name="newScale">The newly confirmed logical-to-device scale.</param>
    /// <exception cref="ArgumentOutOfRangeException">A scale is not finite or is not positive.</exception>
    public DpiChangedEventArgs(double oldScale, double newScale)
    {
        if (!double.IsFinite(oldScale) || oldScale <= 0) throw new ArgumentOutOfRangeException(nameof(oldScale));
        if (!double.IsFinite(newScale) || newScale <= 0) throw new ArgumentOutOfRangeException(nameof(newScale));
        OldScale = oldScale;
        NewScale = newScale;
    }

    /// <summary>Gets the previously confirmed logical-to-device scale, where 1 means 100 percent.</summary>
    public double OldScale { get; }

    /// <summary>Gets the newly confirmed logical-to-device scale, already readable from Scaling.</summary>
    public double NewScale { get; }

    /// <summary>Gets the previous scale multiplied by 96 and truncated to integer DPI.</summary>
    public int OldDpi => (int)(OldScale * 96);

    /// <summary>Gets the new scale multiplied by 96, using the same conversion as Control.DeviceDpi.</summary>
    public int NewDpi => (int)(NewScale * 96);
}
