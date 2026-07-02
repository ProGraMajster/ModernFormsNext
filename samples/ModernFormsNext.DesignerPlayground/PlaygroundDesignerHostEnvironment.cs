using ModernFormsNext.Designer;

namespace ModernFormsNext.DesignerPlayground;

internal sealed class PlaygroundDesignerHostEnvironment : IDesignerHostEnvironment
{
    public string? CurrentDocumentPath { get; private set; }

    public string? CurrentProjectPath => null;

    public void ReportStatus(string message)
    {
        Console.WriteLine(message);
    }

    public void ReportOutput(string message)
    {
        Console.WriteLine(message);
    }
}
