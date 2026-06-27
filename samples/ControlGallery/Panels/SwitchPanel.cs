using ModernFormsNext;
using SkiaSharp;
using MfnDrawing = ModernFormsNext.Drawing;

namespace ControlGallery.Panels
{
    public class SwitchPanel : Panel
    {
        public SwitchPanel()
        {
            var basicLabel = Controls.Add(new Label
            {
                Text = "Boolean switch",
                Left = 10,
                Top = 12,
                Width = 160,
                Height = 24
            });

            var basic = Controls.Add(new Switch
            {
                Left = 180,
                Top = 10,
                IsToggled = true
            });

            var basicValue = Controls.Add(new Label
            {
                Text = "On",
                Left = 250,
                Top = 12,
                Width = 80,
                Height = 24
            });

            basic.Toggled += (_, e) => basicValue.Text = e.Value ? "On" : "Off";

            Controls.Add(new Label
            {
                Text = "Theme icons",
                Left = 10,
                Top = 62,
                Width = 160,
                Height = 24
            });

            Controls.Add(new Switch
            {
                Left = 180,
                Top = 56,
                Width = 72,
                Height = 34,
                OffIcon = SwitchIconKind.Moon,
                OnIcon = SwitchIconKind.Sun,
                OffTrackColor = new SKColor(37, 42, 56),
                OnTrackColor = new SKColor(255, 188, 66),
                OffIconColor = new SKColor(211, 221, 255),
                OnIconColor = new SKColor(80, 48, 0),
                ThumbColor = new SKColor(82, 88, 100),
                ThumbBorderColor = new SKColor(255, 255, 255, 180),
                ThumbBorderWidth = 1,
                AnimationSpeed = 1.35,
                IsToggled = true
            });

            var negativeBrush = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 1)
            };

            negativeBrush.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(88, 70, 180), 0f));
            negativeBrush.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(204, 61, 94), 1f));

            var positiveBrush = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 0)
            };

            positiveBrush.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(0, 120, 212), 0f));
            positiveBrush.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(31, 176, 122), 1f));

            Controls.Add(new Label
            {
                Text = "Three position",
                Left = 10,
                Top = 112,
                Width = 160,
                Height = 24
            });

            var triStateValue = Controls.Add(new Label
            {
                Text = "Value: 0",
                Left = 285,
                Top = 112,
                Width = 100,
                Height = 24
            });

            var triState = Controls.Add(new Switch
            {
                Mode = SwitchMode.ThreeState,
                ActivationMode = SwitchActivationMode.SetByPointerPosition,
                Left = 180,
                Top = 104,
                Width = 88,
                Height = 34,
                Value = 0,
                NegativeIcon = SwitchIconKind.Minus,
                OffIcon = SwitchIconKind.Dot,
                OnIcon = SwitchIconKind.Check,
                NegativeTrackBrush = negativeBrush,
                OffTrackColor = Theme.ControlMidColor,
                OnTrackBrush = positiveBrush,
                NegativeIconColor = SKColors.White,
                OffIconColor = new SKColor(40, 40, 40, 180),
                OnIconColor = SKColors.White
            });

            triState.ValueChanged += (_, e) => triStateValue.Text = $"Value: {e.NewValue}";

            var offGradient = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 1)
            };

            offGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(230, 233, 240), 0f));
            offGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(190, 198, 215), 1f));

            var onGradient = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 0)
            };

            onGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(0, 122, 204), 0f));
            onGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(0, 190, 150), 1f));

            var thumbGradient = new MfnDrawing.LinearGradientBrush
            {
                StartPoint = new SKPoint(0, 0),
                EndPoint = new SKPoint(1, 1)
            };

            thumbGradient.GradientStops.Add(new MfnDrawing.GradientStop(SKColors.White, 0f));
            thumbGradient.GradientStops.Add(new MfnDrawing.GradientStop(new SKColor(92, 111, 148), 1f));

            Controls.Add(new Label
            {
                Text = "Gradient track",
                Left = 10,
                Top = 162,
                Width = 160,
                Height = 24
            });

            Controls.Add(new Switch
            {
                Left = 180,
                Top = 154,
                Width = 92,
                Height = 36,
                ThumbSize = 28,
                OffTrackBrush = offGradient,
                OnTrackBrush = onGradient,
                OffIcon = SwitchIconKind.Cross,
                OnIcon = SwitchIconKind.Check,
                OnIconColor = SKColors.White,
                OffIconColor = new SKColor(70, 70, 70, 180),
                ThumbBorderWidth = 2,
                ThumbBorderColor = SKColors.White,
                ThumbBrush = thumbGradient,
                AnimationSpeed = 0.75
            });

            Controls.Add(new Label
            {
                Text = "Custom icons",
                Left = 10,
                Top = 212,
                Width = 160,
                Height = 24
            });

            Controls.Add(new Switch
            {
                Left = 180,
                Top = 206,
                Width = 82,
                Height = 34,
                OffTrackColor = new SKColor(42, 48, 62),
                OnTrackColor = new SKColor(21, 145, 110),
                TrackBorderColor = new SKColor(15, 22, 33),
                ThumbColor = new SKColor(245, 248, 255),
                ThumbBorderColor = new SKColor(255, 255, 255, 180),
                OffIconImage = CreateLightningIcon(new SKColor(118, 196, 255), new SKColor(255, 214, 94)),
                OnIconImage = CreateLeafIcon(new SKColor(186, 247, 216), new SKColor(47, 121, 88)),
                ThumbIconImage = CreateThumbGlyph(new SKColor(34, 42, 58)),
                IconSize = 18,
                AnimationSpeed = 2.2,
                IsToggled = true
            });

            Controls.Add(new Label
            {
                Text = "No animation",
                Left = 10,
                Top = 262,
                Width = 160,
                Height = 24
            });

            Controls.Add(new Switch
            {
                Left = 180,
                Top = 256,
                Animate = false,
                OffTrackColor = Theme.ControlMidColor,
                OnTrackColor = Theme.AccentColor2,
                ThumbIcon = SwitchIconKind.Dot,
                ThumbIconColor = Theme.AccentColor2
            });
        }

        private static SKBitmap CreateLightningIcon(SKColor primary, SKColor accent)
        {
            var bitmap = new SKBitmap(32, 32, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            using var bolt = new SKPath();
            bolt.MoveTo(18, 2);
            bolt.LineTo(8, 18);
            bolt.LineTo(16, 18);
            bolt.LineTo(12, 30);
            bolt.LineTo(25, 12);
            bolt.LineTo(17, 12);
            bolt.Close();

            using var paint = new SKPaint
            {
                Color = primary,
                IsAntialias = true,
                IsStroke = false
            };

            canvas.DrawPath(bolt, paint);

            paint.Color = accent;
            canvas.DrawCircle(24, 8, 3, paint);

            return bitmap;
        }

        private static SKBitmap CreateLeafIcon(SKColor primary, SKColor accent)
        {
            var bitmap = new SKBitmap(32, 32, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            using var leaf = new SKPath();
            leaf.MoveTo(5, 18);
            leaf.CubicTo(9, 5, 23, 4, 28, 8);
            leaf.CubicTo(28, 22, 17, 28, 5, 18);
            leaf.Close();

            using var paint = new SKPaint
            {
                Color = primary,
                IsAntialias = true,
                IsStroke = false
            };

            canvas.DrawPath(leaf, paint);

            paint.Color = accent;
            paint.IsStroke = true;
            paint.StrokeWidth = 2;
            paint.StrokeCap = SKStrokeCap.Round;
            canvas.DrawLine(9, 19, 24, 10, paint);

            return bitmap;
        }

        private static SKBitmap CreateThumbGlyph(SKColor color)
        {
            var bitmap = new SKBitmap(32, 32, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);

            using var paint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                IsStroke = false
            };

            canvas.DrawCircle(16, 16, 5, paint);
            paint.IsStroke = true;
            paint.StrokeWidth = 2;
            paint.StrokeCap = SKStrokeCap.Round;
            canvas.DrawLine(16, 5, 16, 9, paint);
            canvas.DrawLine(16, 23, 16, 27, paint);
            canvas.DrawLine(5, 16, 9, 16, paint);
            canvas.DrawLine(23, 16, 27, 16, paint);

            return bitmap;
        }
    }
}
