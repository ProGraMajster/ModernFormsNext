namespace ModernFormsNext.Designing;

/// <summary>
/// Hides a property or field from ModernFormsNext designer tooling.
/// </summary>
/// <remarks>
/// This attribute has higher precedence than both <see cref="DesignablePropertyAttribute"/>
/// and standard <see cref="System.ComponentModel"/> attributes. A hidden property is not
/// shown in property UI and is not considered manually editable.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = true)]
public sealed class DesignerHiddenAttribute : Attribute
{
}
