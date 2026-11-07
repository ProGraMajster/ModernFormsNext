namespace ModernFormsNext.Drawing;

/// <summary>Represents a Boolean operation on two shareable filled geometries.</summary>
/// <remarks>
/// Operand transforms are applied before combination; this geometry's transform is applied afterward.
/// Null operands represent empty areas. Open contours are implicitly closed for area operations;
/// stroke widths do not participate. Mutate on the UI thread while rendered. Cycles are rejected.
/// </remarks>
public sealed class CombinedGeometry : Geometry
{
    private Geometry? geometry1;
    private Geometry? geometry2;
    private GeometryCombineMode mode;
    private WeakGeometrySubscription<CombinedGeometry>? firstSubscription;
    private WeakGeometrySubscription<CombinedGeometry>? secondSubscription;

    /// <summary>Initializes a union of empty operands.</summary>
    public CombinedGeometry() { }
    /// <summary>Initializes a Boolean combination of borrowed geometries.</summary>
    /// <param name="mode">The filled-area operation.</param>
    /// <param name="geometry1">The first operand, or null for empty.</param>
    /// <param name="geometry2">The second operand, or null for empty.</param>
    public CombinedGeometry(GeometryCombineMode mode, Geometry? geometry1, Geometry? geometry2)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        GeometryGraph.RequireAcyclic(this, geometry1);
        GeometryGraph.RequireAcyclic(this, geometry2);
        this.mode = mode;
        this.geometry1 = geometry1;
        this.geometry2 = geometry2;
        Subscribe();
    }
    /// <summary>Gets or sets the operation on the operands' filled areas.</summary>
    public GeometryCombineMode Mode
    {
        get => mode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (mode == value) return;
            mode = value;
            OnChanged();
        }
    }
    /// <summary>Gets or sets the borrowed first operand; null is an empty area.</summary>
    public Geometry? Geometry1
    {
        get => geometry1;
        set => ReplaceOperand(ref geometry1, value);
    }
    /// <summary>Gets or sets the borrowed second operand; null is an empty area.</summary>
    public Geometry? Geometry2
    {
        get => geometry2;
        set => ReplaceOperand(ref geometry2, value);
    }
    private void ReplaceOperand(ref Geometry? field, Geometry? value)
    {
        if (ReferenceEquals(field, value)) return;
        GeometryGraph.RequireAcyclic(this, value);
        firstSubscription?.Dispose();
        secondSubscription?.Dispose();
        field = value;
        Subscribe();
        OnChanged();
    }
    private void Subscribe()
    {
        firstSubscription = geometry1 is null ? null : new(this, geometry1, static geometry => geometry.OnChanged());
        secondSubscription = geometry2 is null || ReferenceEquals(geometry1, geometry2)
            ? null : new(this, geometry2, static geometry => geometry.OnChanged());
    }
}
