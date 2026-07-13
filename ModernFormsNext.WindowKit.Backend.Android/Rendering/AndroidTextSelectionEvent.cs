namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes a selection range requested by an Android input method.</summary>
/// <param name="Start">The inclusive UTF-16 selection start.</param>
/// <param name="End">The exclusive UTF-16 selection end.</param>
public readonly record struct AndroidTextSelectionEvent(int Start, int End);
