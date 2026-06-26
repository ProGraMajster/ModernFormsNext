using System;
using System.ComponentModel;
using System.Threading.Tasks;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform.Services;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a common dialog box that lets the user select an installed system font.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shared <see cref="FontDialog"/> API mirrors the WinForms dialog where practical,
    /// while the actual dialog UI is supplied by the active platform backend. The Windows
    /// backend uses the native Win32 font common dialog.
    /// </para>
    /// <para>
    /// The selected <see cref="Font"/> is portable ModernFormsNext data. Assign it to
    /// <see cref="Control.Font"/> or <see cref="ControlStyle.TextFont"/> to preserve the full
    /// style, including underline and strikeout effects.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var dialog = new FontDialog
    /// {
    ///     Font = new Font("Segoe UI", 12),
    ///     ShowColor = true,
    ///     ShowApply = true
    /// };
    ///
    /// dialog.Apply += (_, _) =>
    /// {
    ///     label.Font = dialog.Font;
    ///     label.Style.ForegroundColor = dialog.Color;
    /// };
    ///
    /// if (await dialog.ShowDialog(this) == DialogResult.OK)
    /// {
    ///     label.Font = dialog.Font;
    ///     label.Style.ForegroundColor = dialog.Color;
    /// }
    /// </code>
    /// </example>
    [DefaultEvent(nameof(Apply))]
    [DefaultProperty(nameof(Font))]
    public class FontDialog : CommonDialog
    {
        private const int DefaultMaxSize = 0;
        private const int DefaultMinSize = 0;

        private Font? font;
        private int maxSize = DefaultMaxSize;
        private int minSize = DefaultMinSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="FontDialog"/> class.
        /// </summary>
        public FontDialog()
        {
            Reset();
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog allows simulated styles such as synthetic bold or italic.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(true)]
        public bool AllowSimulations { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vector fonts can be selected.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(true)]
        public bool AllowVectorFonts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether vertical fonts are shown by the dialog.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(true)]
        public bool AllowVerticalFonts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user can change the selected character script.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(true)]
        public bool AllowScriptChange { get; set; }

        /// <summary>
        /// Gets or sets the selected text color.
        /// </summary>
        /// <remarks>
        /// The Windows backend exposes this value through the native color list when
        /// <see cref="ShowColor"/> and <see cref="ShowEffects"/> allow the color controls to be visible.
        /// </remarks>
        public SKColor Color { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether only fixed-pitch fonts can be selected.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(false)]
        public bool FixedPitchOnly { get; set; }

        /// <summary>
        /// Gets or sets the selected font.
        /// </summary>
        /// <remarks>
        /// The returned font size is clamped to <see cref="MinSize"/> and <see cref="MaxSize"/>
        /// when those limits are set. Setting this property does not invalidate controls by itself;
        /// callers decide where and how the selected font is applied.
        /// </remarks>
        public Font Font
        {
            get
            {
                var result = font ?? GetDefaultFont();

                if (minSize != DefaultMinSize && result.SizeInPoints < minSize)
                    result = new Font(result.FamilyName, minSize, result.Style);

                if (maxSize != DefaultMaxSize && result.SizeInPoints > maxSize)
                    result = new Font(result.FamilyName, maxSize, result.Style);

                return result;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                font = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog should require an existing font selection.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(false)]
        public bool FontMustExist { get; set; }

        /// <summary>
        /// Gets or sets how the dialog user interface is rendered.
        /// </summary>
        /// <value>
        /// One of the <see cref="FontDialogRenderingMode"/> values. The default is
        /// <see cref="FontDialogRenderingMode.System"/> to preserve native-dialog behavior
        /// for WinForms migration scenarios.
        /// </value>
        /// <remarks>
        /// <para>
        /// <see cref="FontDialogRenderingMode.System"/> uses the active platform backend and may
        /// expose platform-specific behavior such as the Windows common font dialog. If the
        /// backend does not provide a system font dialog, this mode throws
        /// <see cref="PlatformNotSupportedException"/>.
        /// </para>
        /// <para>
        /// <see cref="FontDialogRenderingMode.ModernFormsNext"/> uses a dialog composed from
        /// ModernFormsNext controls. It visually matches the framework and supports the common
        /// font, style, size, color, Apply, and Help behaviors, but some Win32-specific filtering
        /// options may be approximated or ignored.
        /// </para>
        /// </remarks>
        [DefaultValue(FontDialogRenderingMode.System)]
        public FontDialogRenderingMode RenderingMode { get; set; }

        /// <summary>
        /// Gets or sets the maximum point size a user can select.
        /// </summary>
        /// <value>
        /// The maximum size in points, or <c>0</c> to use the platform default.
        /// </value>
        /// <remarks>
        /// Negative values are normalized to <c>0</c>. If the maximum becomes smaller than
        /// <see cref="MinSize"/>, <see cref="MinSize"/> is reduced to match it.
        /// </remarks>
        [DefaultValue(DefaultMaxSize)]
        public int MaxSize
        {
            get => maxSize;
            set
            {
                maxSize = Math.Max(0, value);

                if (maxSize > 0 && maxSize < minSize)
                    minSize = maxSize;
            }
        }

        /// <summary>
        /// Gets or sets the minimum point size a user can select.
        /// </summary>
        /// <value>
        /// The minimum size in points, or <c>0</c> to use the platform default.
        /// </value>
        /// <remarks>
        /// Negative values are normalized to <c>0</c>. If the minimum becomes larger than
        /// <see cref="MaxSize"/>, <see cref="MaxSize"/> is raised to match it.
        /// </remarks>
        [DefaultValue(DefaultMinSize)]
        public int MinSize
        {
            get => minSize;
            set
            {
                minSize = Math.Max(0, value);

                if (maxSize > 0 && maxSize < minSize)
                    maxSize = minSize;
            }
        }

        /// <summary>
        /// Gets a bit mask representing the Windows-compatible option state for diagnostic and derived-dialog use.
        /// </summary>
        /// <remarks>
        /// The value follows the Win32 <c>CHOOSEFONT</c> flag layout for options represented by this dialog.
        /// It is provided for WinForms-style migration scenarios. Portable code should prefer the named
        /// properties instead of depending on individual bits.
        /// </remarks>
        protected int Options => BuildOptions();

        /// <summary>
        /// Gets or sets a value indicating whether the dialog allows only non-OEM or symbol character sets and ANSI fonts.
        /// </summary>
        /// <remarks>
        /// This option is currently honored by the Windows backend. Other backends may ignore it
        /// until equivalent platform support is added.
        /// </remarks>
        [DefaultValue(false)]
        public bool ScriptsOnly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays an Apply button.
        /// </summary>
        /// <remarks>
        /// When supported by the backend, clicking Apply updates <see cref="Font"/> and
        /// <see cref="Color"/> before raising <see cref="Apply"/>.
        /// </remarks>
        [DefaultValue(false)]
        public bool ShowApply { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays color selection controls.
        /// </summary>
        /// <remarks>
        /// On Windows, color selection is part of the effects area. If <see cref="ShowEffects"/>
        /// is <see langword="false"/>, the native dialog may not display color controls even when
        /// this property is <see langword="true"/>.
        /// </remarks>
        [DefaultValue(false)]
        public bool ShowColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays controls for underline, strikeout, and related effects.
        /// </summary>
        /// <remarks>
        /// This option affects dialog UI. ModernFormsNext controls must still opt in to drawing
        /// underline and strikeout effects after a font is selected.
        /// </remarks>
        [DefaultValue(true)]
        public bool ShowEffects { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dialog displays a Help button.
        /// </summary>
        /// <remarks>
        /// Backend support varies by platform. The Windows backend forwards native help commands
        /// to <see cref="CommonDialog.HelpRequest"/> when available.
        /// </remarks>
        [DefaultValue(false)]
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Occurs when the user clicks the Apply button in the dialog.
        /// </summary>
        /// <remarks>
        /// The <see cref="Font"/> and <see cref="Color"/> properties are updated before this event
        /// is raised. Apply is currently supported by the Windows backend.
        /// </remarks>
        public event EventHandler? Apply;

        /// <inheritdoc/>
        public override void Reset()
        {
            AllowSimulations = true;
            AllowVectorFonts = true;
            AllowVerticalFonts = true;
            AllowScriptChange = true;
            Color = Theme.ForegroundColor;
            FixedPitchOnly = false;
            font = null;
            FontMustExist = false;
            maxSize = DefaultMaxSize;
            minSize = DefaultMinSize;
            ScriptsOnly = false;
            ShowApply = false;
            ShowColor = false;
            ShowEffects = true;
            ShowHelp = false;
            RenderingMode = FontDialogRenderingMode.System;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{base.ToString()}, Font: {Font}";

        /// <summary>
        /// Raises the <see cref="Apply"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnApply(EventArgs e) => Apply?.Invoke(this, e);

        /// <inheritdoc/>
        protected override async Task<DialogResult> RunDialog(Form owner)
        {
            return RenderingMode switch
            {
                FontDialogRenderingMode.Auto => await RunAutoDialog(owner),
                FontDialogRenderingMode.System => await RunSystemDialog(owner),
                FontDialogRenderingMode.ModernFormsNext => await RunModernFormsDialog(owner),
                _ => throw new InvalidEnumArgumentException(nameof(RenderingMode), (int)RenderingMode, typeof(FontDialogRenderingMode))
            };
        }

        private async Task<DialogResult> RunAutoDialog(Form owner)
        {
            FrameworkBootstrap.EnsureInitialized();
            var service = AvaloniaGlobals.GetService<IPlatformFontDialogService>();

            if (service is null)
                return await RunModernFormsDialog(owner);

            return await RunSystemDialog(owner);
        }

        private async Task<DialogResult> RunSystemDialog(Form owner)
        {
            FrameworkBootstrap.EnsureInitialized();

            var service = AvaloniaGlobals.GetService<IPlatformFontDialogService>();

            if (service is null)
                throw new PlatformNotSupportedException("The current platform backend does not provide a font dialog service.");

            var result = await service.ShowFontDialogAsync(owner.window, CreatePlatformOptions());

            if (result is null)
                return DialogResult.Cancel;

            UpdateFromPlatformResult(result);
            return DialogResult.OK;
        }

        private async Task<DialogResult> RunModernFormsDialog(Form owner)
        {
            var form = new FontDialogForm(
                Font,
                Color,
                minSize,
                maxSize,
                ShowApply,
                ShowColor,
                ShowEffects,
                ShowHelp,
                (selectedFont, selectedColor) =>
                {
                    font = selectedFont;
                    Color = selectedColor;
                    OnApply(EventArgs.Empty);
                },
                () => OnHelpRequest(EventArgs.Empty));

            var result = await form.ShowDialog(owner);

            if (result == DialogResult.OK)
            {
                font = form.SelectedFont;
                Color = form.SelectedColor;
            }

            return result;
        }

        private static Font GetDefaultFont()
        {
            var typeface = Theme.UIFont;
            return new Font(typeface.FamilyName, Theme.FontSize);
        }

        private int BuildOptions()
        {
            var options = PlatformFontDialogRequest.ScreenFontsFlag | PlatformFontDialogRequest.TrueTypeOnlyFlag;

            if (ShowEffects)
                options |= PlatformFontDialogRequest.EffectsFlag;

            if (ShowApply)
                options |= PlatformFontDialogRequest.ApplyFlag;

            if (ShowHelp)
                options |= PlatformFontDialogRequest.ShowHelpFlag;

            if (!AllowSimulations)
                options |= PlatformFontDialogRequest.NoSimulationsFlag;

            if (!AllowVectorFonts)
                options |= PlatformFontDialogRequest.NoVectorFontsFlag;

            if (!AllowVerticalFonts)
                options |= PlatformFontDialogRequest.NoVerticalFontsFlag;

            if (!AllowScriptChange)
                options |= PlatformFontDialogRequest.SelectScriptFlag;

            if (FixedPitchOnly)
                options |= PlatformFontDialogRequest.FixedPitchOnlyFlag;

            if (FontMustExist)
                options |= PlatformFontDialogRequest.ForceFontExistFlag;

            if (ScriptsOnly)
                options |= PlatformFontDialogRequest.ScriptsOnlyFlag;

            if (minSize > 0 || maxSize > 0)
                options |= PlatformFontDialogRequest.LimitSizeFlag;

            return options;
        }

        private PlatformFontDialogRequest CreatePlatformOptions()
        {
            return new PlatformFontDialogRequest
            {
                AllowScriptChange = AllowScriptChange,
                AllowSimulations = AllowSimulations,
                AllowVectorFonts = AllowVectorFonts,
                AllowVerticalFonts = AllowVerticalFonts,
                Apply = result =>
                {
                    UpdateFromPlatformResult(result);
                    OnApply(EventArgs.Empty);
                },
                Color = Color,
                FixedPitchOnly = FixedPitchOnly,
                Font = ToPlatformFont(Font),
                FontMustExist = FontMustExist,
                HelpRequest = () => OnHelpRequest(EventArgs.Empty),
                MaxSize = maxSize,
                MinSize = minSize,
                ScriptsOnly = ScriptsOnly,
                ShowApply = ShowApply,
                ShowColor = ShowColor,
                ShowEffects = ShowEffects,
                ShowHelp = ShowHelp,
                TrueTypeOnly = true
            };
        }

        private static PlatformFontSelection ToPlatformFont(Font value)
        {
            return new PlatformFontSelection(
                value.FamilyName,
                value.SizeInPoints,
                ToPlatformStyle(value.Style));
        }

        private static PlatformFontStyle ToPlatformStyle(FontStyle style)
        {
            var result = PlatformFontStyle.Regular;

            if (style.HasFlag(FontStyle.Bold))
                result |= PlatformFontStyle.Bold;

            if (style.HasFlag(FontStyle.Italic))
                result |= PlatformFontStyle.Italic;

            if (style.HasFlag(FontStyle.Underline))
                result |= PlatformFontStyle.Underline;

            if (style.HasFlag(FontStyle.Strikeout))
                result |= PlatformFontStyle.Strikeout;

            return result;
        }

        private static FontStyle ToFontStyle(PlatformFontStyle style)
        {
            var result = FontStyle.Regular;

            if (style.HasFlag(PlatformFontStyle.Bold))
                result |= FontStyle.Bold;

            if (style.HasFlag(PlatformFontStyle.Italic))
                result |= FontStyle.Italic;

            if (style.HasFlag(PlatformFontStyle.Underline))
                result |= FontStyle.Underline;

            if (style.HasFlag(PlatformFontStyle.Strikeout))
                result |= FontStyle.Strikeout;

            return result;
        }

        private void UpdateFromPlatformResult(PlatformFontDialogResult result)
        {
            font = new Font(result.Font.FamilyName, result.Font.SizeInPoints, ToFontStyle(result.Font.Style));
            Color = result.Color;
        }
    }
}
