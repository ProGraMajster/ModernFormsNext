namespace ModernFormsNext.WindowKit.Platform.Accessibility;

/// <summary>
/// Supplies the additional canonical semantics consumed by native UI Automation backends without
/// expanding the public WindowKit accessibility contract.
/// </summary>
internal interface IPlatformUiaAccessibleObject : IPlatformAccessibleObject
{
    long RuntimeId { get; }

    string? AutomationId { get; }

    int ControlType { get; }

    int View { get; }

    string? ClassName { get; }

    bool IsSensitive { get; }

    PlatformAccessibleRangeValue? RangeValue { get; }

    int SupportedActions { get; }

    bool PerformAction(int action, object? parameter = null);
}

/// <summary>
/// Carries canonical numeric range metadata across the internal framework/backend boundary.
/// </summary>
internal readonly record struct PlatformAccessibleRangeValue(
    double Value,
    double Minimum,
    double Maximum,
    double SmallChange,
    double LargeChange,
    bool IsReadOnly);

internal static class PlatformUiaAccessibleObjectExtensions
{
    public static long GetRuntimeId(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.RuntimeId ?? 0;

    public static string? GetAutomationId(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.AutomationId;

    public static int GetControlType(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.ControlType ?? 0;

    public static int GetAccessibilityView(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.View ?? 0;

    public static string? GetClassName(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.ClassName;

    public static bool GetIsSensitive(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.IsSensitive ?? false;

    public static PlatformAccessibleRangeValue? GetRangeValue(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.RangeValue;

    public static int GetSupportedActions(this IPlatformAccessibleObject value)
        => (value as IPlatformUiaAccessibleObject)?.SupportedActions ?? 0;

    public static bool PerformUiaAction(
        this IPlatformAccessibleObject value,
        int action,
        object? parameter = null)
        => value is IPlatformUiaAccessibleObject uiaValue
            && uiaValue.PerformAction(action, parameter);
}
