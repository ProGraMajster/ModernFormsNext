namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes an Android editing-key transition.</summary>
/// <param name="Key">The platform-neutral editing or navigation key.</param>
/// <param name="IsDown"><see langword="true"/> for key down; <see langword="false"/> for key up.</param>
public readonly record struct AndroidInputKeyEvent(AndroidInputKey Key, bool IsDown);
