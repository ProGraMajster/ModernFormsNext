namespace ModernFormsNext.WindowKit.Backend.Android.Rendering;

/// <summary>Describes text and caret placement requested by an Android input method.</summary>
/// <param name="Text">The complete committed or composing text.</param>
/// <param name="NewCursorPosition">
/// The cursor position relative to the text: positive values are relative to the end minus one;
/// zero or negative values are relative to the start.
/// </param>
/// <remarks>
/// <paramref name="Text"/> uses .NET UTF-16 code units. The event contains no Android objects so
/// a platform host can forward it into the shared framework input pipeline unchanged.
/// </remarks>
public readonly record struct AndroidTextEditEvent(string Text, int NewCursorPosition);
