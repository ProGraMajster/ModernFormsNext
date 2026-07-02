namespace ModernFormsNext.VisualStudioExtension.Detection;

internal sealed class ModernFormsDesignableFileInfo
{
    public ModernFormsDesignableFileInfo(
        string codeFilePath,
        string designerCodePath,
        string designFilePath,
        string className,
        bool hasDesignFile,
        bool hasProjectDesignMetadata,
        bool isDesignable)
    {
        CodeFilePath = codeFilePath;
        DesignerCodePath = designerCodePath;
        DesignFilePath = designFilePath;
        ClassName = className;
        HasDesignFile = hasDesignFile;
        HasProjectDesignMetadata = hasProjectDesignMetadata;
        IsDesignable = isDesignable;
    }

    public string CodeFilePath { get; }

    public string DesignerCodePath { get; }

    public string DesignFilePath { get; }

    public string ClassName { get; }

    public bool HasDesignFile { get; }

    public bool HasProjectDesignMetadata { get; }

    public bool IsDesignable { get; }
}
