namespace ModernFormsNext.Designing;

/// <summary>Provides the authoritative detached descriptors for built-in interaction effects.</summary>
public static class BuiltInAnimationDefinitionCatalog
{
    /// <summary>Gets the namespace-qualified RippleEffect type name.</summary>
    public const string RippleEffectTypeName = "ModernFormsNext.Animations.RippleEffect";
    /// <summary>Gets the namespace-qualified PressScaleEffect type name.</summary>
    public const string PressScaleEffectTypeName = "ModernFormsNext.Animations.PressScaleEffect";

    /// <summary>Gets built-in effect descriptors in Designer display order.</summary>
    public static IReadOnlyList<DesignAnimationDefinitionDescriptor> Definitions { get; } =
    [
        new(
            RippleEffectTypeName,
            "RippleEffect",
            DesignAnimationDefinitionKind.InteractionEffect,
            [
                Boolean("Enabled", true),
                Color("ColorArgb", "Color", unchecked((int)0x5AFFFFFF)),
                TimeSpan("DurationMilliseconds", "Duration", 450d),
                Easing("Easing", "CubicOut"),
                Boolean("StartFromPointer", true),
                Enum("RadiusMode", "ModernFormsNext.Animations.RippleRadiusMode", "CoverControl", "CoverControl", "Fixed"),
                Number("FixedRadius", 48d, 0d, float.MaxValue),
                Enum("Layer", "ModernFormsNext.Animations.RippleLayer", "AboveBackgroundBelowContent", "AboveBackgroundBelowContent", "AboveContent"),
                Integer("MaxConcurrentRipples", 4, 1, 32),
                Enum("OverflowPolicy", "ModernFormsNext.Animations.RippleOverflowPolicy", "RemoveOldest", "RemoveOldest", "RemoveNewest", "IgnoreNew", "ReplaceAll")
            ]) { IsBuiltIn = true },
        new(
            PressScaleEffectTypeName,
            "PressScaleEffect",
            DesignAnimationDefinitionKind.InteractionEffect,
            [
                Boolean("Enabled", true),
                Number("PressedScale", 0.97d, float.Epsilon, float.MaxValue),
                TimeSpan("PressDurationMilliseconds", "PressDuration", 80d),
                TimeSpan("ReleaseDurationMilliseconds", "ReleaseDuration", 120d),
                Easing("Easing", "CubicOut")
            ]) { IsBuiltIn = true }
    ];

    private static DesignAnimationPropertyDescriptor Boolean(string name, bool value)
        => new(name, name, DesignAnimationPropertyKind.Boolean, DesignPropertyValue.FromBoolean(value));

    private static DesignAnimationPropertyDescriptor Integer(string name, int value, int minimum, int maximum)
        => new(name, name, DesignAnimationPropertyKind.Int32, DesignPropertyValue.FromInt32(value))
        { Minimum = minimum, Maximum = maximum };

    private static DesignAnimationPropertyDescriptor Number(string name, double value, double minimum, double maximum)
        => new(name, name, DesignAnimationPropertyKind.Number, DesignPropertyValue.FromDouble(value))
        { Minimum = minimum, Maximum = maximum, RuntimeTypeName = "float" };

    private static DesignAnimationPropertyDescriptor TimeSpan(string name, string runtimeName, double milliseconds)
        => new(name, runtimeName, DesignAnimationPropertyKind.TimeSpan, DesignPropertyValue.FromDouble(milliseconds))
        { Minimum = 0d };

    private static DesignAnimationPropertyDescriptor Easing(string name, string value)
        => new(name, name, DesignAnimationPropertyKind.Easing, DesignPropertyValue.FromString(value));

    private static DesignAnimationPropertyDescriptor Color(string name, string runtimeName, int value)
        => new(name, runtimeName, DesignAnimationPropertyKind.ColorArgb, DesignPropertyValue.FromInt32(value));

    private static DesignAnimationPropertyDescriptor Enum(
        string name,
        string typeName,
        string defaultMember,
        params string[] members)
        => new(name, name, DesignAnimationPropertyKind.Enum, DesignPropertyValue.FromEnum(typeName, defaultMember))
        { EnumTypeName = typeName, EnumMembers = members };
}
