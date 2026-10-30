using System.Reflection;

namespace ModernFormsNext;

/// <summary>
/// Identifies a resource target through statically referenced CLR getter and setter delegates.
/// </summary>
/// <typeparam name="TControl">The control type that declares or inherits the property.</typeparam>
/// <typeparam name="TValue">The exact CLR property type, including its nullability.</typeparam>
/// <remarks>
/// Reuse one descriptor for a property. Delegates run on the calling UI thread and must access the
/// ordinary property so its layout, rendering, and validation behavior is preserved. No reflection,
/// expression compilation, dynamic code, or global registration is performed. The descriptor does
/// not retain a target control. Captured delegate state remains owned by the application.
/// </remarks>
/// <example>
/// <code>
/// static readonly ResourceProperty&lt;Control, SkiaSharp.SKColor&gt; Background = new(
///     nameof(Control.BackColor), static control => control.BackColor,
///     static (control, value) => control.BackColor = value);
/// // The getter and setter are visible to the trimmer.
/// button.SetResourceReference(Background, "Button.Background");
/// </code>
/// </example>
public sealed class ResourceProperty<TControl, TValue> where TControl : Control
{
    /// <summary>Creates a reusable, reflection-free resource property descriptor.</summary>
    /// <param name="name">The case-sensitive CLR property name, also used to clear the reference.</param>
    /// <param name="getter">Reads the ordinary CLR property on the supplied control.</param>
    /// <param name="setter">Writes the ordinary CLR property on the supplied control.</param>
    /// <exception cref="ArgumentNullException">A name or delegate is null.</exception>
    /// <exception cref="ArgumentException">The name is empty or whitespace.</exception>
    public ResourceProperty(string name, Func<TControl, TValue> getter, Action<TControl, TValue> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        Name = name;
        Accessor = new TypedAccessor(name, getter, setter);
    }

    /// <summary>Gets the case-sensitive property name used by resource references and diagnostics.</summary>
    public string Name { get; }

    internal ResourcePropertyAccessor Accessor { get; }

    private sealed class TypedAccessor(string name, Func<TControl, TValue> getter, Action<TControl, TValue> setter)
        : ResourcePropertyAccessor(name, typeof(TValue))
    {
        public override object? GetValue(Control control) => getter((TControl)control);
        public override void SetValue(Control control, object? value) => setter((TControl)control, (TValue)value!);
    }
}

// Both entry points share the existing binding, weak subscriptions, fallback and normal setters.
internal abstract class ResourcePropertyAccessor(string name, Type propertyType)
{
    public string Name { get; } = name;
    public Type PropertyType { get; } = propertyType;
    public abstract object? GetValue(Control control);
    public abstract void SetValue(Control control, object? value);
}

internal sealed class ReflectedResourcePropertyAccessor(PropertyInfo property)
    : ResourcePropertyAccessor(property.Name, property.PropertyType)
{
    public override object? GetValue(Control control) => property.GetValue(control);
    public override void SetValue(Control control, object? value) => property.SetValue(control, value);
}
