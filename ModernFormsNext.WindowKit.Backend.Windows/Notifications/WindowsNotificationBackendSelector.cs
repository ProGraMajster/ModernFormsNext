using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Pure backend selection inputs; tests can supply these without loading native libraries.</summary>
/// <param name="Version">Actual OS version/build, not a marketing release name.</param>
/// <param name="AppSdkAvailable">Whether the optional App SDK provider and native support probe succeeded.</param>
/// <param name="ClassicAvailable">Whether WinRT notification APIs and application identity are available.</param>
/// <param name="ShellAvailable">Whether Shell notification APIs are available.</param>
/// <param name="Elevated">Whether the process is elevated.</param>
public sealed record WindowsNotificationEnvironment(Version Version, bool AppSdkAvailable, bool ClassicAvailable, bool ShellAvailable, bool Elevated);

/// <summary>Selects a backend using actual native availability, with version gates before optional loading.</summary>
public static class WindowsNotificationBackendSelector
{
    /// <summary>Returns a backend without loading libraries or mutating app identity.</summary>
    public static WindowsNotificationBackendKind Select(WindowsNotificationEnvironment environment, WindowsNotificationRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        bool modern = !environment.Elevated && environment.Version >= new Version(10, 0, 17763) && environment.AppSdkAvailable;
        bool classic = !environment.Elevated && environment.Version >= new Version(6, 2) && environment.ClassicAvailable;
        bool shell = environment.Version >= new Version(6, 1) && environment.ShellAvailable;
        return options.Backend switch
        {
            WindowsNotificationBackendKind.AppSdk => modern ? WindowsNotificationBackendKind.AppSdk : WindowsNotificationBackendKind.Unavailable,
            WindowsNotificationBackendKind.Classic => classic ? WindowsNotificationBackendKind.Classic : WindowsNotificationBackendKind.Unavailable,
            WindowsNotificationBackendKind.Shell => shell ? WindowsNotificationBackendKind.Shell : WindowsNotificationBackendKind.Unavailable,
            WindowsNotificationBackendKind.Automatic => modern ? WindowsNotificationBackendKind.AppSdk : classic ? WindowsNotificationBackendKind.Classic :
                shell && options.AllowShellFallback ? WindowsNotificationBackendKind.Shell : WindowsNotificationBackendKind.Unavailable,
            _ => WindowsNotificationBackendKind.Unavailable
        };
    }

    /// <summary>Computes XML features from OS build and probed API properties. Does not claim runtime certification.</summary>
    public static SystemNotificationCapabilities GetToastCapabilities(Version version, bool packaged, bool updateApi, bool historyApi,
        bool urgent, bool styles, bool tooltips, bool coldActivation, bool priorityApi)
    {
        var f = SystemNotificationFeatures.Basic | SystemNotificationFeatures.Images | SystemNotificationFeatures.Removal;
        var w = WindowsSystemNotificationFeatures.LoopingAudio;
        if (packaged) f |= SystemNotificationFeatures.ResourceImages;
        if (historyApi) f |= SystemNotificationFeatures.History | SystemNotificationFeatures.Grouping | SystemNotificationFeatures.Replacement | SystemNotificationFeatures.GroupRemoval | SystemNotificationFeatures.Clear;
        if (coldActivation) f |= SystemNotificationFeatures.ColdActivation;
        if (version >= new Version(10, 0, 10240))
        {
            f |= SystemNotificationFeatures.Actions | SystemNotificationFeatures.UriActions |
                SystemNotificationFeatures.Inputs | SystemNotificationFeatures.Expiration;
            w |= WindowsSystemNotificationFeatures.InlineImages | WindowsSystemNotificationFeatures.AppLogo |
                WindowsSystemNotificationFeatures.Scenarios | WindowsSystemNotificationFeatures.RawXml | WindowsSystemNotificationFeatures.SuppressPopup;
            if (packaged) f |= SystemNotificationFeatures.CustomAudio | SystemNotificationFeatures.RemoteImages;
        }
        if (version >= new Version(10, 0, 14393))
            w |= WindowsSystemNotificationFeatures.HeroImages | WindowsSystemNotificationFeatures.Attribution | WindowsSystemNotificationFeatures.ContextMenuActions;
        if (version >= new Version(10, 0, 15063))
        {
            w |= WindowsSystemNotificationFeatures.Headers;
            f |= SystemNotificationFeatures.Timestamp;
            if (updateApi) f |= SystemNotificationFeatures.Progress | SystemNotificationFeatures.LiveUpdates | SystemNotificationFeatures.IndeterminateProgress;
        }
        if (urgent) w |= WindowsSystemNotificationFeatures.UrgentScenario;
        if (styles) w |= WindowsSystemNotificationFeatures.ButtonStyles;
        if (tooltips) w |= WindowsSystemNotificationFeatures.ButtonTooltips;
        if (priorityApi) w |= WindowsSystemNotificationFeatures.Priority;
        if (priorityApi) f |= SystemNotificationFeatures.Urgency;
        return new("Windows toast", f)
        {
            Platform = new WindowsSystemNotificationCapabilities(w),
            Limits = new() { TextBlocks = 3, Images = version.Major < 10 ? 1 : 3, Actions = 5, Inputs = 5, Choices = 5, InputsPerAction = 5 }
        };
    }
}
