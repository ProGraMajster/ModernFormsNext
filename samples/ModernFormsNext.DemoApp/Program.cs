using ModernFormsNext;

namespace ModernFormsNext.DemoApp;

/// <summary>
/// Provides the application entry point for the ModernFormsNext reference application.
/// </summary>
/// <remarks>
/// This project represents the default application structure generated for users by the
/// ModernFormsNext Visual Studio template. Keep it minimal, clean, and beginner-friendly.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Starts the ModernFormsNext reference application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.Run(new MainForm());
    }
}