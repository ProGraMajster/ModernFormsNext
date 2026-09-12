using System.Drawing;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Platform;
using SkiaSharp;
using DrawingColor = System.Drawing.Color;
using Size = System.Drawing.Size;

namespace ModernFormsNext.CrossPlatform.Sample;

public sealed partial class MainPage
{
    private CheckBox preferenceOptIn = null!;
    private Label preferenceStatus = null!;
    private IPlatformAccessibilitySettings? preferenceProvider;
    private EventHandler<PlatformAccessibilityPreferences>? preferenceHandler;
    private PlatformAccessibilityPreferences? appliedPreferences;
    private long preferenceGeneration;
    private bool preferencePosted, preferenceDisposed, preferenceLayoutEnabled;

    private void InitializePreferenceDemo()
    {
        preferenceOptIn = new CheckBox { Name = "FollowAccessibilityPreferences", Text = "Use system accessibility preferences" };
        preferenceStatus = CreateLabel("Accessibility preferences: opt-in is off; application theme remains authored.");
        preferenceStatus.Name = "AccessibilityPreferenceStatus";
        preferenceStatus.Multiline = true;
        preferenceOptIn.CheckedChanged += (_, _) =>
        {
            ReleasePreferenceObservation();
            if (preferenceDisposed || Disposing) return;
            if (!preferenceOptIn.Checked)
            {
                preferenceStatus.Text = "Accessibility preferences: following is off; current application theme is retained.";
                return;
            }
            preferenceProvider = AvaloniaGlobals.GetService<IPlatformSettings>() as IPlatformAccessibilitySettings;
            if (preferenceProvider is null)
            {
                preferenceStatus.Text = "Accessibility preferences: detection unknown (legacy or unavailable provider).";
                return;
            }
            long version = preferenceGeneration;
            preferenceHandler = (_, _) => QueuePreferenceApply(version);
            try
            {
                preferenceProvider.AccessibilityPreferencesChanged += preferenceHandler;
                QueuePreferenceApply(version);
            }
            catch { ReleasePreferenceObservation(); throw; }
        };
    }

    private void QueuePreferenceApply(long version)
    {
        if (!IsCurrentPreference(version) || preferencePosted) return;
        preferencePosted = true;
        try
        {
            app.PlatformServices.Dispatcher.Post(() =>
            {
                if (!IsCurrentPreference(version)) return;
                preferencePosted = false;
                var snapshot = preferenceProvider!.GetAccessibilityPreferences();
                if (!IsCurrentPreference(version) || snapshot == appliedPreferences) return;
                // Commit the observed identity before Apply callbacks can synchronously request
                // another refresh. The immutable authored definition is rebuilt, never rescaled.
                appliedPreferences = snapshot;
                var theme = CreatePreferenceTheme(snapshot.ColorValues);
                var result = ThemeManager.Current.Apply(theme, new ThemeApplyOptions { TextScale = snapshot.TextScale ?? 1d });
                if (!IsCurrentPreference(version)) return;
                if (!result.Success)
                {
                    appliedPreferences = null;
                    preferenceStatus.Text = "Accessibility theme application failed; the previous theme remains active.";
                    return;
                }
                preferenceLayoutEnabled = true;
                BackColor = ToSkia(result.Snapshot!.Get(ThemeTokens.Colors.Background));
                scrollArea.BackColor = BackColor;
                preferenceStatus.Text = $"Accessibility: contrast={snapshot.ColorValues?.ContrastPreference.ToString() ?? "unknown"}; " +
                    $"text scale={snapshot.TextScale?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}. " +
                    "Theme fonts scale once; explicit fonts stay authored.";
                ArrangeControls();
            });
        }
        catch { preferencePosted = false; throw; }
    }

    private bool IsCurrentPreference(long version) => !preferenceDisposed && !Disposing &&
        preferenceGeneration == version && preferenceProvider is not null && preferenceOptIn.Checked;

    private void ReleasePreferenceObservation()
    {
        preferenceGeneration++;
        preferencePosted = false;
        appliedPreferences = null;
        var provider = preferenceProvider;
        var handler = preferenceHandler;
        preferenceProvider = null;
        preferenceHandler = null;
        if (provider is not null && handler is not null) provider.AccessibilityPreferencesChanged -= handler;
    }

    private void DisposePreferenceDemo()
    {
        preferenceDisposed = true;
        ReleasePreferenceObservation();
    }

    private static ThemeDefinition CreatePreferenceTheme(PlatformColorValues? colors)
    {
        bool high = colors?.ContrastPreference == ColorContrastPreference.High;
        var theme = colors?.ThemeVariant == PlatformThemeVariant.Dark ? BuiltInThemes.Dark : BuiltInThemes.Light;
        theme.Id = high ? "sample.accessible.high-contrast" : "sample.accessible.normal";
        theme.Name = high ? "Sample high contrast" : "Sample normal";
        // Supply explicit authored typography so the example does not depend on installed defaults.
        foreach (var entry in BuiltInThemes.Base.Typography) theme.Typography[entry.Key] = entry.Value;
        if (!high) return theme;
        DrawingColor background = colors!.BackgroundColor ?? DrawingColor.Black;
        DrawingColor foreground = colors.ForegroundColor ?? DrawingColor.White;
        DrawingColor accent = colors.BackgroundColor is null ? DrawingColor.Yellow : colors.AccentColor1;
        DrawingColor onAccent = colors.BackgroundColor is null ? DrawingColor.Black : colors.AccentColor2;
        foreach (string key in new[] { "Background", "Surface", "SurfaceVariant" }) theme.Colors[key] = background;
        foreach (string key in new[] { "TextPrimary", "TextSecondary", "Border", "Divider" }) theme.Colors[key] = foreground;
        theme.Colors["TextDisabled"] = foreground; // State is also semantic; keep the label readable.
        foreach (string key in new[] { "Primary", "PrimaryHover", "PrimaryPressed", "Secondary", "Accent", "Focus" }) theme.Colors[key] = accent;
        theme.Colors["PrimaryText"] = onAccent;
        // Text selection keeps the editor's foreground; choose a contrasting backdrop for it.
        theme.Colors["Selection"] = foreground.GetBrightness() > .5f ? DrawingColor.FromArgb(0, 51, 102) : DrawingColor.FromArgb(180, 220, 255);
        return theme;
    }

    private static SKColor ToSkia(DrawingColor color) => new(color.R, color.G, color.B, color.A);

    private void MeasurePreferenceRow(Control control, ref int width, ref int height)
    {
        if (preferenceLayoutEnabled && control is FlowLayoutPanel panel)
        {
            int requiredHeight = panel.Padding.Vertical + 16;
            foreach (Control child in panel.Controls)
            {
                int childWidth = Math.Max(1, width - 16), childHeight = child.Height;
                MeasurePreferenceRow(child, ref childWidth, ref childHeight);
                child.Size = new Size(childWidth, childHeight);
                width = Math.Max(width, childWidth + 16);
                requiredHeight += childHeight + child.Margin.Vertical + 8;
            }
            height = Math.Max(height, requiredHeight);
            return;
        }
        if (!preferenceLayoutEnabled) return;
        // An empty editor still renders a caret and accepts the next line at the current
        // theme size. Its minimum cannot depend on whether the document has text yet.
        if (control is TextBox)
        {
            var measured = TextMeasurer.MeasureText("Ag", control);
            height = Math.Max(height, (int)Math.Ceiling(measured.Height / control.ScaleFactor.Height) + 16);
            return;
        }
        if (string.IsNullOrEmpty(control.Text)) return;
        double scale = control.ScaleFactor.Width;
        int allowance = control is CheckBox ? 34 : 16;
        if (control is Label label)
        {
            label.Multiline = true;
            var measured = TextMeasurer.MeasureText(control.Text, control,
                new Size(Math.Max(1, (int)((width - 16) * scale)), int.MaxValue));
            height = Math.Max(height, (int)Math.Ceiling(measured.Height / control.ScaleFactor.Height) + 12);
        }
        else if (control is Button or CheckBox)
        {
            var measured = TextMeasurer.MeasureText(control.Text, control, new Size(int.MaxValue, int.MaxValue));
            width = Math.Max(width, (int)Math.Ceiling(measured.Width / scale) + allowance);
            height = Math.Max(height, (int)Math.Ceiling(measured.Height / control.ScaleFactor.Height) + 16);
        }
    }
}
