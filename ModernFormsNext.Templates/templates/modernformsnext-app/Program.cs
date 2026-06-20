using ModernFormsNext;

namespace MyApp;

/// <summary>
/// Provides the application entry point.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the ModernFormsNext application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.Run(new MainForm());
    }
}