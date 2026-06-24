using System;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;

namespace ModernFormsNext.WindowKit.Platform.Services
{
    /// <summary>
    /// Provides platform-specific font dialog support for shared ModernFormsNext dialog APIs.
    /// </summary>
    /// <remarks>
    /// Implementations should show the native or backend-appropriate font selection UI for the
    /// current platform. The service receives and returns portable font data so that shared
    /// framework code does not depend on Win32 handles, native structures, or backend types.
    /// </remarks>
    public interface IPlatformFontDialogService
    {
        /// <summary>
        /// Shows a modal font selection dialog owned by the specified window.
        /// </summary>
        /// <param name="owner">The platform window that owns the dialog.</param>
        /// <param name="request">The initial font, color, and option state for the dialog.</param>
        /// <returns>
        /// A task whose result is the selected font data, or <see langword="null"/> when the
        /// user cancels the dialog.
        /// </returns>
        /// <remarks>
        /// Implementations should run on the UI thread. Backends that use native modal dialogs
        /// may complete the returned task only after the native dialog closes.
        /// </remarks>
        Task<PlatformFontDialogResult?> ShowFontDialogAsync(IWindowBaseImpl owner, PlatformFontDialogRequest request);
    }

    /// <summary>
    /// Specifies style information for a platform-neutral font dialog selection.
    /// </summary>
    [Flags]
    public enum PlatformFontStyle
    {
        /// <summary>
        /// Normal text without bold, italic, underline, or strikeout effects.
        /// </summary>
        Regular = 0,

        /// <summary>
        /// Bold text.
        /// </summary>
        Bold = 1,

        /// <summary>
        /// Italic text.
        /// </summary>
        Italic = 2,

        /// <summary>
        /// Underlined text.
        /// </summary>
        Underline = 4,

        /// <summary>
        /// Text drawn with a strikeout line.
        /// </summary>
        Strikeout = 8
    }

    /// <summary>
    /// Describes a font family, point size, and style used by platform dialog services.
    /// </summary>
    /// <param name="FamilyName">The font family name.</param>
    /// <param name="SizeInPoints">The font size in points.</param>
    /// <param name="Style">The selected font style.</param>
    public sealed record PlatformFontSelection(
        string FamilyName,
        float SizeInPoints,
        PlatformFontStyle Style);

    /// <summary>
    /// Contains option data for a platform font dialog request.
    /// </summary>
    /// <remarks>
    /// The Boolean properties intentionally mirror WinForms and Win32 font dialog concepts.
    /// Backends should honor the values they can support and ignore unsupported options
    /// rather than failing the dialog.
    /// </remarks>
    public sealed class PlatformFontDialogRequest
    {
        /// <summary>
        /// Gets Win32-compatible flag bits for screen font selection.
        /// </summary>
        public const int ScreenFontsFlag = 0x00000001;

        /// <summary>
        /// Gets Win32-compatible flag bits for displaying a Help button.
        /// </summary>
        public const int ShowHelpFlag = 0x00000004;

        /// <summary>
        /// Gets Win32-compatible flag bits for displaying effects controls.
        /// </summary>
        public const int EffectsFlag = 0x00000100;

        /// <summary>
        /// Gets Win32-compatible flag bits for displaying an Apply button.
        /// </summary>
        public const int ApplyFlag = 0x00000200;

        /// <summary>
        /// Gets Win32-compatible flag bits for script-only font selection.
        /// </summary>
        public const int ScriptsOnlyFlag = 0x00000400;

        /// <summary>
        /// Gets Win32-compatible flag bits for hiding vector fonts.
        /// </summary>
        public const int NoVectorFontsFlag = 0x00000800;

        /// <summary>
        /// Gets Win32-compatible flag bits for disabling simulated styles.
        /// </summary>
        public const int NoSimulationsFlag = 0x00001000;

        /// <summary>
        /// Gets Win32-compatible flag bits for limiting selectable sizes.
        /// </summary>
        public const int LimitSizeFlag = 0x00002000;

        /// <summary>
        /// Gets Win32-compatible flag bits for fixed-pitch-only selection.
        /// </summary>
        public const int FixedPitchOnlyFlag = 0x00004000;

        /// <summary>
        /// Gets Win32-compatible flag bits for requiring an existing font.
        /// </summary>
        public const int ForceFontExistFlag = 0x00010000;

        /// <summary>
        /// Gets Win32-compatible flag bits for TrueType-only selection.
        /// </summary>
        public const int TrueTypeOnlyFlag = 0x00040000;

        /// <summary>
        /// Gets Win32-compatible flag bits for script selection behavior.
        /// </summary>
        public const int SelectScriptFlag = 0x00400000;

        /// <summary>
        /// Gets Win32-compatible flag bits for hiding vertical fonts.
        /// </summary>
        public const int NoVerticalFontsFlag = 0x01000000;

        /// <summary>
        /// Gets or sets a value indicating whether the user can change the selected character script.
        /// </summary>
        public bool AllowScriptChange { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether simulated styles are allowed.
        /// </summary>
        public bool AllowSimulations { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether vector fonts are allowed.
        /// </summary>
        public bool AllowVectorFonts { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether vertical fonts are allowed.
        /// </summary>
        public bool AllowVerticalFonts { get; set; } = true;

        /// <summary>
        /// Gets or sets the callback invoked when a platform Apply command is accepted.
        /// </summary>
        /// <remarks>
        /// The callback is expected to run on the UI thread. Implementations should invoke it
        /// after reading the dialog's current selection.
        /// </remarks>
        public Action<PlatformFontDialogResult>? Apply { get; set; }

        /// <summary>
        /// Gets or sets the initial and selected color.
        /// </summary>
        public SKColor Color { get; set; } = SKColors.Black;

        /// <summary>
        /// Gets or sets a value indicating whether only fixed-pitch fonts can be selected.
        /// </summary>
        public bool FixedPitchOnly { get; set; }

        /// <summary>
        /// Gets or sets the initial font selection.
        /// </summary>
        public PlatformFontSelection Font { get; set; } = new("Segoe UI", 9, PlatformFontStyle.Regular);

        /// <summary>
        /// Gets or sets a value indicating whether the platform should reject nonexistent font selections.
        /// </summary>
        public bool FontMustExist { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the platform dialog reports a help request.
        /// </summary>
        public Action? HelpRequest { get; set; }

        /// <summary>
        /// Gets or sets the maximum selectable font size in points, or <c>0</c> for the platform default.
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Gets or sets the minimum selectable font size in points, or <c>0</c> for the platform default.
        /// </summary>
        public int MinSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only script-capable fonts should be shown.
        /// </summary>
        public bool ScriptsOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the platform dialog should display an Apply button.
        /// </summary>
        public bool ShowApply { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the platform dialog should display color controls.
        /// </summary>
        public bool ShowColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the platform dialog should display effects controls.
        /// </summary>
        public bool ShowEffects { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the platform dialog should display a Help button.
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the platform dialog should restrict selection to TrueType fonts.
        /// </summary>
        public bool TrueTypeOnly { get; set; } = true;
    }

    /// <summary>
    /// Contains the result returned by a platform font dialog.
    /// </summary>
    /// <param name="Font">The selected font.</param>
    /// <param name="Color">The selected text color.</param>
    public sealed record PlatformFontDialogResult(
        PlatformFontSelection Font,
        SKColor Color);
}
