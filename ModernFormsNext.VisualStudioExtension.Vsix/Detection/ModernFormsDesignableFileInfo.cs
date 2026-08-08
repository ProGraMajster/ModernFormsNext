namespace ModernFormsNext.VisualStudioExtension.Detection;

internal sealed class ModernFormsDesignableFileInfo
{
    public ModernFormsDesignableFileInfo(
        string codeFilePath,
        string designerCodePath,
        string designFilePath,
        string? namespaceName,
        string className,
        bool isUserControl,
        bool hasDesignFile,
        bool hasProjectDesignMetadata,
        bool isDesignable)
    {
        CodeFilePath = codeFilePath;
        DesignerCodePath = designerCodePath;
        DesignFilePath = designFilePath;
        NamespaceName = namespaceName;
        ClassName = className;
        IsUserControl = isUserControl;
        HasDesignFile = hasDesignFile;
        HasProjectDesignMetadata = hasProjectDesignMetadata;
        IsDesignable = isDesignable;
    }

    public string CodeFilePath { get; }

    public string DesignerCodePath { get; }

    public string DesignFilePath { get; }

    public string? NamespaceName { get; }

    public string ClassName { get; }

    public bool IsUserControl { get; }

    public bool HasDesignFile { get; }

    public bool HasProjectDesignMetadata { get; }

    public bool IsDesignable { get; }
}
