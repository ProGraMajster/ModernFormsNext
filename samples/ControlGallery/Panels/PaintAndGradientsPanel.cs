using System;
using System.Numerics;
using ModernFormsNext;
using ModernFormsNext.Drawing;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;

namespace ControlGallery.Panels;

/// <summary>
/// Provides a manual visual test surface for solid, gradient, transformed, and resource-backed brushes.
/// </summary>
public sealed class PaintAndGradientsPanel : Panel
{
    private const string DynamicBrushKey = "ControlGallery.Paint.Dynamic";
    private readonly LinearGradientBrush dynamicBrush;
    private readonly Panel dynamicCard;
    private bool alternateState;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaintAndGradientsPanel"/> class.
    /// </summary>
    public PaintAndGradientsPanel()
    {
        AutoScroll = true;

        Controls.Add(new Label
        {
            Left = 24,
            Top = 18,
            Width = 720,
            Height = 30,
            Text = "Paint and gradients",
            Font = new Font("Segoe UI", 16)
        });
        Controls.Add(new Label
        {
            Left = 24,
            Top = 52,
            Width = 760,
            Height = 42,
            Multiline = true,
            Text = "Relative coordinates follow each control's bounds. The bottom card uses a dynamic resource; mutate or replace it without reassigning BackgroundBrush."
        });

        AddCard(24, 108, "Solid + opacity", new SolidColorBrush(Color.CornflowerBlue) { Opacity = 0.72f });
        AddCard(280, 108, "Linear / Reflect", CreateLinearGradient(GradientSpreadMode.Reflect));
        AddCard(536, 108, "Radial + focal origin", CreateRadialGradient());
        AddCard(24, 248, "Sweep", CreateSweepGradient());
        AddCard(280, 248, "Transform", CreateTransformedGradient());

        dynamicBrush = CreateDynamicGradient();
        Resources[DynamicBrushKey] = dynamicBrush;
        dynamicCard = AddCard(536, 248, "Dynamic resource", brush: null);
        dynamicCard.SetResourceReference(nameof(Control.BackgroundBrush), DynamicBrushKey);

        var mutateButton = Controls.Add(new Button
        {
            Left = 536,
            Top = 390,
            Width = 118,
            Height = 32,
            Text = "Mutate stops"
        });
        mutateButton.Click += (_, _) => MutateDynamicBrush();

        var replaceButton = Controls.Add(new Button
        {
            Left = 662,
            Top = 390,
            Width = 118,
            Height = 32,
            Text = "Replace resource"
        });
        replaceButton.Click += (_, _) => ReplaceDynamicResource();

        Controls.Add(new Label
        {
            Left = 24,
            Top = 446,
            Width = 756,
            Height = 72,
            Multiline = true,
            Text = "Manual checks: resize the gallery, verify smooth gradients and stable relative geometry, then use both buttons. Changes should repaint immediately without flashes, stale shaders, or a second DPI scale."
        });
    }

    private Panel AddCard(int left, int top, string caption, ModernFormsNext.Drawing.Brush brush)
    {
        var card = Controls.Add(new Panel
        {
            Left = left,
            Top = top,
            Width = 232,
            Height = 112,
            BackgroundBrush = brush
        });
        card.Style.Border.Width = 1;
        card.Style.Border.Color = Theme.BorderMidColor;
        card.Controls.Add(new Label
        {
            Left = 10,
            Top = 76,
            Width = 210,
            Height = 25,
            Text = caption,
            BackColor = new SkiaSharp.SKColor(255, 255, 255, 210)
        });
        return card;
    }

    private static LinearGradientBrush CreateLinearGradient(GradientSpreadMode spreadMode)
    {
        var brush = new LinearGradientBrush
        {
            Start = new PointF(0f, 0f),
            End = new PointF(0.38f, 0f),
            SpreadMode = spreadMode
        };
        brush.GradientStops.AddRange([
            new GradientStop(Color.FromArgb(255, 22, 93, 173), 0f),
            new GradientStop(Color.FromArgb(255, 91, 219, 195), 0.55f),
            new GradientStop(Color.FromArgb(255, 248, 194, 74), 1f)
        ]);
        return brush;
    }

    private static RadialGradientBrush CreateRadialGradient()
    {
        var brush = new RadialGradientBrush
        {
            CenterPoint = new PointF(0.55f, 0.52f),
            GradientOrigin = new PointF(0.28f, 0.28f),
            Radius = 0.72f
        };
        brush.GradientStops.AddRange([
            new GradientStop(Color.White, 0f),
            new GradientStop(Color.DeepSkyBlue, 0.42f),
            new GradientStop(Color.MidnightBlue, 1f)
        ]);
        return brush;
    }

    private static SweepGradientBrush CreateSweepGradient()
    {
        var brush = new SweepGradientBrush
        {
            CenterPoint = new PointF(0.5f, 0.5f),
            StartAngle = -45f,
            EndAngle = 315f
        };
        brush.GradientStops.AddRange([
            new GradientStop(Color.Crimson, 0f),
            new GradientStop(Color.Gold, 0.25f),
            new GradientStop(Color.MediumSeaGreen, 0.5f),
            new GradientStop(Color.RoyalBlue, 0.75f),
            new GradientStop(Color.Crimson, 1f)
        ]);
        return brush;
    }

    private static LinearGradientBrush CreateTransformedGradient()
    {
        var brush = CreateLinearGradient(GradientSpreadMode.Repeat);
        brush.End = new PointF(0.3f, 0f);
        brush.Transform = Matrix3x2.CreateRotation(0.22f, new Vector2(116f, 56f));
        return brush;
    }

    private static LinearGradientBrush CreateDynamicGradient()
    {
        var brush = new LinearGradientBrush { Start = new PointF(0f, 0f), End = new PointF(1f, 1f) };
        brush.GradientStops.AddRange([
            new GradientStop(Color.MediumPurple, 0f),
            new GradientStop(Color.HotPink, 0.5f),
            new GradientStop(Color.Orange, 1f)
        ]);
        return brush;
    }

    private void MutateDynamicBrush()
    {
        alternateState = !alternateState;
        dynamicBrush.GradientStops[0].PaintColor = alternateState ? Color.Teal : Color.MediumPurple;
        dynamicBrush.GradientStops[1].Offset = alternateState ? 0.72f : 0.5f;
        dynamicBrush.Opacity = alternateState ? 0.68f : 1f;
    }

    private void ReplaceDynamicResource()
    {
        alternateState = !alternateState;
        Resources[DynamicBrushKey] = alternateState
            ? CreateRadialGradient()
            : CreateDynamicGradient();
    }
}
