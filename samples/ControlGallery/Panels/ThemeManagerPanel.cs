using System;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Animations;

namespace ControlGallery.Panels;

/// <summary>
/// Provides opt-in manual checks for theme loading, inheritance, resources, and transitions.
/// </summary>
public sealed class ThemeManagerPanel : BasePanel
{
    private const string CustomThemeJson = """
        {
          "schemaVersion": 1,
          "id": "controlgallery.custom",
          "name": "ControlGallery Custom",
          "description": "An inherited JSON theme used only by ControlGallery.",
          "author": "ModernFormsNext",
          "baseTheme": "modernformsnext.light",
          "variant": "Custom",
          "metadata": { "source": "ControlGallery" },
          "tags": [ "demo", "json" ],
          "colors": {
            "Background": "#FFF4F0FF",
            "Primary": "#FF7846D8",
            "PrimaryHover": "#FF8E62E0",
            "PrimaryPressed": "#FF6031BC",
            "Accent": "#FFE05B9B"
          },
          "brushes": {
            "SurfaceBrush": {
              "type": "solid",
              "color": "#FFFFFBFF",
              "opacity": 1,
              "transform": [ 1, 0, 0, 1, 0, 0 ]
            },
            "PrimaryGradient": {
              "type": "linearGradient",
              "gradientStops": [
                { "color": "#FF7846D8", "offset": 0 },
                { "color": "#FFE05B9B", "offset": 1 }
              ],
              "spreadMode": "Pad",
              "start": [ 0, 0 ],
              "end": [ 1, 1 ],
              "opacity": 1,
              "transform": [ 1, 0, 0, 1, 0, 0 ]
            }
          },
          "typography": {
            "Title": { "fontFamily": "Segoe UI", "size": 24, "style": "Bold" }
          },
          "spacing": { "GalleryGap": 12 },
          "padding": {
            "GalleryCard": { "left": 16, "top": 12, "right": 16, "bottom": 12 }
          },
          "corners": { "GalleryCard": 10 },
          "animations": {
            "ThemeTransition": { "durationMs": 500, "easing": "EaseInOut", "enabled": true }
          },
          "resources": {
            "Description": { "type": "string", "value": "Loaded from allow-listed JSON" }
          }
        }
        """;

    private readonly ThemeManager manager = ThemeManager.Current;
    private readonly AnimationScheduler scheduler = AnimationScheduler.Default;
    private readonly ThemeJsonSerializer serializer = new();
    private readonly Label diagnosticsLabel;
    private readonly Label resultLabel;
    private readonly Label semanticLabel;
    private readonly Button animationsButton;
    private readonly Button reducedMotionButton;
    private readonly bool originalAnimationsEnabled;
    private readonly bool originalReducedMotion;
    private bool unloaded;

    /// <summary>Initializes the ThemeManager manual validation page.</summary>
    public ThemeManagerPanel()
    {
        AutoScroll = true;
        originalAnimationsEnabled = scheduler.Policy.AnimationsEnabled;
        originalReducedMotion = scheduler.Policy.ReducedMotion;
        manager.ThemeChanged += HandleThemeChanged;
        manager.ThemeTransitionCompleted += HandleTransitionCompleted;
        manager.ThemeApplyFailed += HandleThemeApplyFailed;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 760,
            Height = 30,
            Text = "ThemeManager: resources, JSON and transitions",
            Font = new ModernFormsNext.Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 780,
            Height = 44,
            Multiline = true,
            Text = "The cards below use dynamic theme Brush resources. Switch repeatedly and verify that controls remain alive while colors, gradients, legacy Theme values and diagnostics update atomically."
        });

        AddResourceCard(24, 108, "SurfaceBrush", "Surface Brush");
        AddResourceCard(280, 108, "PrimaryGradient", "Primary Gradient");
        AddResourceCard(536, 108, "SurfaceGlow", "Surface Glow");

        AddButton(24, 252, 118, "Light now", () => Apply(BuiltInThemes.Light, animated: false));
        AddButton(150, 252, 118, "Dark now", () => Apply(BuiltInThemes.Dark, animated: false));
        AddButton(276, 252, 132, "Light animated", () => Apply(BuiltInThemes.Light, animated: true));
        AddButton(416, 252, 132, "Dark animated", () => Apply(BuiltInThemes.Dark, animated: true));
        AddButton(556, 252, 108, "Rapid x3", RapidSwitch);
        AddButton(672, 252, 104, "Cancel", CancelTransition);

        animationsButton = AddButton(24, 294, 176, string.Empty, ToggleAnimations);
        reducedMotionButton = AddButton(208, 294, 176, string.Empty, ToggleReducedMotion);
        AddButton(392, 294, 122, "Load JSON", LoadCustomJson);
        AddButton(522, 294, 122, "Bad JSON", LoadMalformedJson);
        AddButton(652, 294, 124, "Missing base", ApplyMissingBase);

        AddButton(24, 336, 176, "Inheritance cycle", ApplyInheritanceCycle);
        AddButton(208, 336, 176, "Refresh diagnostics", UpdateDiagnostics);
        AddButton(392, 336, 176, "Bad Brush JSON", LoadInvalidBrushJson);

        semanticLabel = Controls.Add(new Label
        {
            Left = 24,
            Top = 388,
            Width = 752,
            Height = 54,
            Multiline = true
        });
        diagnosticsLabel = Controls.Add(new Label
        {
            Left = 24,
            Top = 450,
            Width = 752,
            Height = 92,
            Multiline = true
        });
        resultLabel = Controls.Add(new Label
        {
            Left = 24,
            Top = 550,
            Width = 752,
            Height = 70,
            Multiline = true,
            Text = "Ready. No file watcher or background service is started by this page."
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 628,
            Width = 752,
            Height = 105,
            Multiline = true,
            Text = "Manual checks: Light→Dark immediate and animated; rapid replacement; cancel; toggle animations or reduced motion mid-transition; leave/re-enter; minimize/restore; resize; load valid/malformed/invalid-Brush JSON; missing base; cycle; inspect gradient transition; confirm active animations return to 0 and the tick source stops. Android background/foreground and Designer isolation require their dedicated hosts."
        });

        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    /// <inheritdoc />
    public override void UnloadPanel()
    {
        if (unloaded)
            return;

        unloaded = true;
        manager.CancelTransition();
        manager.ThemeChanged -= HandleThemeChanged;
        manager.ThemeTransitionCompleted -= HandleTransitionCompleted;
        manager.ThemeApplyFailed -= HandleThemeApplyFailed;
        scheduler.Policy.AnimationsEnabled = originalAnimationsEnabled;
        scheduler.Policy.ReducedMotion = originalReducedMotion;
        base.UnloadPanel();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            UnloadPanel();
        base.Dispose(disposing);
    }

    private void AddResourceCard(int left, int top, string tokenName, string caption)
    {
        var card = Controls.Add(new Panel
        {
            Left = left,
            Top = top,
            Width = 232,
            Height = 112
        });
        card.SetResourceReference(
            nameof(Control.BackgroundBrush),
            ThemeResourceKeys.Create(ThemeTokenCategory.Brush, tokenName));
        card.Controls.Add(new Label
        {
            Left = 10,
            Top = 76,
            Width = 210,
            Height = 25,
            Text = caption,
            BackColor = new SkiaSharp.SKColor(255, 255, 255, 220)
        });
    }

    private Button AddButton(int left, int top, int width, string text, Action action)
    {
        var button = Controls.Add(new Button
        {
            Left = left,
            Top = top,
            Width = width,
            Height = 32,
            Text = text
        });
        button.Click += (_, _) => action();
        return button;
    }

    private void Apply(ThemeDefinition theme, bool animated)
    {
        ThemeApplyResult result = manager.Apply(theme, Options(animated));
        resultLabel.Text = result.Success
            ? $"Committed '{result.Snapshot!.Name}'. ThemeChanged means commit; transition completion is reported separately."
            : FormatDiagnostics(result.Diagnostics);
        UpdateDiagnostics();
    }

    private void RapidSwitch()
    {
        Apply(BuiltInThemes.Dark, animated: true);
        Apply(BuiltInThemes.Light, animated: true);
        Apply(BuiltInThemes.Dark, animated: true);
    }

    private void CancelTransition()
    {
        resultLabel.Text = manager.CancelTransition()
            ? "Cancellation requested; the committed target values will be snapped into place."
            : "No active theme transition to cancel.";
        UpdateDiagnostics();
    }

    private void ToggleAnimations()
    {
        scheduler.Policy.AnimationsEnabled = !scheduler.Policy.AnimationsEnabled;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void ToggleReducedMotion()
    {
        scheduler.Policy.ReducedMotion = !scheduler.Policy.ReducedMotion;
        UpdatePolicyButtons();
        UpdateDiagnostics();
    }

    private void LoadCustomJson()
    {
        try
        {
            Apply(serializer.Deserialize(CustomThemeJson), animated: true);
        }
        catch (ThemeSerializationException exception)
        {
            resultLabel.Text = exception.Message;
        }
    }

    private void LoadMalformedJson()
    {
        try
        {
            _ = serializer.Deserialize("{ \"schemaVersion\": 1, \"id\": ");
        }
        catch (ThemeSerializationException exception)
        {
            resultLabel.Text = $"Expected safe JSON error: {exception.Message}";
        }
        UpdateDiagnostics();
    }

    private void LoadInvalidBrushJson()
    {
        try
        {
            _ = serializer.Deserialize("""
                {
                  "schemaVersion": 1,
                  "id": "controlgallery.bad-brush",
                  "name": "Bad Brush",
                  "brushes": {
                    "Card": { "type": "arbitraryClrType" }
                  }
                }
                """);
        }
        catch (ThemeSerializationException exception)
        {
            resultLabel.Text = $"Expected Brush allow-list error: {exception.Message}";
        }
        UpdateDiagnostics();
    }

    private void ApplyMissingBase()
    {
        var theme = new ThemeDefinition("controlgallery.missing-base", "Missing Base")
        {
            BaseTheme = "controlgallery.not-registered"
        };
        Apply(theme, animated: false);
    }

    private void ApplyInheritanceCycle()
    {
        var first = new ThemeDefinition("controlgallery.cycle-a", "Cycle A")
        {
            BaseTheme = "controlgallery.cycle-b"
        };
        var second = new ThemeDefinition("controlgallery.cycle-b", "Cycle B")
        {
            BaseTheme = first.Id
        };
        manager.Register(first, replace: true);
        manager.Register(second, replace: true);
        Apply(first, animated: false);
    }

    private void HandleThemeChanged(object sender, ThemeChangedEventArgs e)
    {
        resultLabel.Text = $"Committed '{e.Current.Name}'. Transition: {e.Transition?.State.ToString() ?? "none"}.";
        UpdateDiagnostics();
    }

    private void HandleTransitionCompleted(object sender, ThemeTransitionCompletedEventArgs e)
    {
        resultLabel.Text = $"Transition for '{e.ThemeId}' ended with {e.Status}.";
        UpdateDiagnostics();
    }

    private void HandleThemeApplyFailed(object sender, ThemeApplyFailedEventArgs e)
    {
        resultLabel.Text = "Apply rejected: " + FormatDiagnostics(e.Result.Diagnostics);
        UpdateDiagnostics();
    }

    private void UpdatePolicyButtons()
    {
        animationsButton.Text = scheduler.Policy.AnimationsEnabled
            ? "Animations: enabled"
            : "Animations: disabled";
        reducedMotionButton.Text = scheduler.Policy.ReducedMotion
            ? "Reduced motion: on"
            : "Reduced motion: off";
    }

    private void UpdateDiagnostics()
    {
        ThemeManagerDiagnostics theme = manager.GetDiagnostics();
        AnimationSchedulerDiagnostics animation = scheduler.GetDiagnostics();
        diagnosticsLabel.Text =
            $"Active: {theme.ActiveThemeName ?? "none"} ({theme.ActiveThemeId ?? "n/a"}) | variant: {theme.ActiveVariant} | schema: {theme.SchemaVersion} | transition: {theme.TransitionState}\n" +
            $"Tokens: {theme.TokenCounts.Total} | bases: {string.Join(" → ", theme.BaseChain)} | switches completed/canceled/failed: {theme.SuccessfulSwitches}/{theme.CanceledSwitches}/{theme.FailedSwitches}\n" +
            $"Scheduler active: {animation.ActiveAnimationCount} | ticks: {animation.TickCount} | tick source: {(animation.IsTickSourceRunning ? "running" : "stopped")} | paused: {animation.IsPaused}";

        ThemeResolvedSnapshot snapshot = manager.ActiveSnapshot;
        if (snapshot is null)
        {
            semanticLabel.Text = "No active snapshot.";
            return;
        }

        string primary = snapshot.Colors.TryGetValue(ThemeTokens.Colors.Primary.Name, out Color color)
            ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
            : "missing";
        string typography = snapshot.Typography.TryGetValue(ThemeTokens.Typography.Title.Name, out ThemeTypography title)
            ? $"{title.FontFamily} {title.Size}pt {title.Style}"
            : "missing";
        snapshot.Spacing.TryGetValue("Medium", out double spacing);
        snapshot.Corners.TryGetValue("Medium", out double corner);
        semanticLabel.Text =
            $"Semantic Primary: {primary} | Title typography: {typography}\n" +
            $"Spacing Medium: {spacing} logical px | Corner Medium: {corner} logical px | Brush tokens: {string.Join(", ", snapshot.BrushTokenNames)}";
    }

    private static ThemeApplyOptions Options(bool animated)
        => new()
        {
            Transition = new ThemeTransitionOptions
            {
                Enabled = animated,
                Duration = TimeSpan.FromMilliseconds(700),
                Easing = ThemeEasing.EaseInOut,
                RespectReducedMotion = true
            }
        };

    private static string FormatDiagnostics(System.Collections.Generic.IReadOnlyList<ThemeDiagnostic> diagnostics)
        => diagnostics.Count == 0
            ? "Theme apply failed without validation diagnostics."
            : string.Join(" | ", System.Linq.Enumerable.Select(
                diagnostics,
                static item => $"{item.Code}: {item.Message}"));
}
