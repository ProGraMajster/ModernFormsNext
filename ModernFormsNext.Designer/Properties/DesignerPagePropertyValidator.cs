using System.Reflection;
using ModernFormsNext.Designing;

namespace ModernFormsNext.Designer.Properties;

// The same trusted concrete types used by Toolbox and preview. Project subtypes,
// route tables, lifecycle state and commands never participate in these editors.
internal static class DesignerPagePropertyValidator
{
    internal static bool IsKnown(Type type) => type == typeof(ContentPage) || type == typeof(PageHost)
        || type == typeof(NavigationPage) || type == typeof(TabbedPage)
        || type == typeof(FlyoutPage) || type == typeof(AppShell);

    internal static bool HideProperty(Type type, PropertyInfo property)
        => IsKnown(type) && (property.Name == "Text"
            || (property.DeclaringType == typeof(Page) || property.DeclaringType is { } owner && IsKnown(owner))
                && property.Name is not ("Title" or "SelectedIndex" or "FlowDirection" or "DisplayMode"
                    or "FlyoutWidth" or "SplitBreakpoint" or "TransitionDuration"));

    internal static bool TryValidate(DesignControlNode node, Type type, out string? error)
    {
        error = DesignPageContainers.GetChildError(node);
        if (error is not null) return false;
        try
        {
            // Setters on exact framework types enforce the production metric/enum rules.
            // In particular, never validate SelectedIndex against an empty runtime default.
            using Control control = type == typeof(FlyoutPage) ? new FlyoutPage()
                : type == typeof(TabbedPage) ? new TabbedPage()
                : type == typeof(NavigationPage) ? new NavigationPage()
                : type == typeof(AppShell) ? new AppShell()
                : type == typeof(PageHost) ? new PageHost() : new ContentPage();
            control.SuspendLayout();
            if (control is FlyoutPage flyout)
            {
                flyout.DisplayMode = Read(node, "DisplayMode", flyout.DisplayMode);
                flyout.FlowDirection = Read(node, "FlowDirection", flyout.FlowDirection);
                flyout.FlyoutWidth = Read(node, "FlyoutWidth", flyout.FlyoutWidth);
                flyout.SplitBreakpoint = Read(node, "SplitBreakpoint", flyout.SplitBreakpoint);
                flyout.TransitionDuration = Read(node, "TransitionDuration", flyout.TransitionDuration);
            }
            if (control is TabbedPage tabs) tabs.FlowDirection = Read(node, "FlowDirection", tabs.FlowDirection);
            if (control is NavigationPage navigation)
                navigation.TransitionDuration = Read(node, "TransitionDuration", navigation.TransitionDuration);
            if (control is AppShell shell)
                shell.TransitionDuration = Read(node, "TransitionDuration", shell.TransitionDuration);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or OverflowException)
        { error = exception.Message; return false; }
    }

    private static T Read<T>(DesignControlNode node, string name, T fallback)
        => node.Properties.TryGetValue(name, out var value)
            ? (T)DesignerPropertyValueEditor.FromDesignPropertyValue(value, typeof(T))! : fallback;
}
