namespace ModernFormsNext.Designer.Services;

internal sealed class DesignerGenerationFileResult
{
    public DesignerGenerationFileResult(bool succeeded, string path, string code, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Path = path;
        Code = code;
        Errors = errors;
    }

    public bool Succeeded { get; }

    public string Path { get; }

    public string Code { get; }

    public IReadOnlyList<string> Errors { get; }
}
