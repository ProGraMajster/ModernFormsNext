namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes text Android asks the framework editor to delete around its caret.</summary>
/// <param name="BeforeLength">The requested number of UTF-16 code units before the caret.</param>
/// <param name="AfterLength">The requested number of UTF-16 code units after the caret.</param>
public readonly record struct AndroidTextDeletionRequest(int BeforeLength, int AfterLength);
