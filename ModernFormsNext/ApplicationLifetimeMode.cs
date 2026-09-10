namespace ModernFormsNext;

/// <summary>Determines when the existing application message loop requests graceful exit.</summary>
/// <remarks>Popups are never counted as independent application windows. Activity recreation does not close this loop.</remarks>
public enum ApplicationLifetimeMode
{
    /// <summary>Exit when the designated Run root closes, preserving the default framework behavior.</summary>
    MainWindowClosed,

    /// <summary>Exit after a Form closes and no open Forms remain; hidden but open Forms still count.</summary>
    LastWindowClosed,

    /// <summary>Keep running until Application.Exit is explicitly requested or the platform ends the loop.</summary>
    Explicit
}
