using System;
using System.Drawing;
using System.Globalization;
using SkiaSharp;

namespace ModernFormsNext.Renderers
{
    internal class DateTimePickerCalendarRenderer : Renderer<DateTimePickerCalendar>
    {
        protected override void Render (DateTimePickerCalendar control, PaintEventArgs e)
        {
            var canvas = e.Canvas;

            using var backgroundPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = control.CurrentStyle.BackgroundColor ?? SKColors.White
            };

            using var borderPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = control.CurrentStyle.Border.Color ?? new SKColor (180, 180, 180)
            };

            using var textPaint = CreateTextPaint (control, control.DisplayMonth.ToString ("Y", CultureInfo.CurrentCulture));
            using var mutedTextPaint = CreateTextPaint (control, "pon.");
            mutedTextPaint.Paint.Color = mutedTextPaint.Paint.Color.WithAlpha (150);

            using var accentPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = Theme.AccentColor
            };

            using var hoverPaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = Blend (control.CurrentStyle.BackgroundColor ?? SKColors.White, SKColors.Black, 0.06f)
            };

            using var todayOutlinePaint = new SKPaint {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                Color = Theme.AccentColor
            };

            canvas.DrawRect (new SKRect (0, 0, control.Width, control.Height), backgroundPaint);
            canvas.DrawRect (new SKRect (0, 0, control.Width, control.Height), borderPaint);

            DrawHeader (control, canvas, textPaint, hoverPaint);

            switch (control.ViewMode) {
                case DateTimePickerCalendarViewMode.Days:
                    DrawDayHeaders (control, canvas, mutedTextPaint);
                    DrawDayCells (control, canvas, textPaint, mutedTextPaint, accentPaint, hoverPaint, borderPaint, todayOutlinePaint);
                    DrawTodayButton (control, canvas, textPaint, hoverPaint, borderPaint);
                    break;

                case DateTimePickerCalendarViewMode.Months:
                    DrawMonthCells (control, canvas, textPaint, hoverPaint, borderPaint, accentPaint);
                    break;

                case DateTimePickerCalendarViewMode.Years:
                    DrawYearCells (control, canvas, textPaint, hoverPaint, borderPaint, accentPaint);
                    break;
            }
        }

        private static void DrawHeader (DateTimePickerCalendar control, SKCanvas canvas, TextPaintResources textPaint, SKPaint hoverPaint)
        {
            DrawButton (canvas, "<", control.PreviousButtonRectangle, textPaint, hoverPaint, control.HoveredRectangle == control.PreviousButtonRectangle);
            DrawButton (canvas, ">", control.NextButtonRectangle, textPaint, hoverPaint, control.HoveredRectangle == control.NextButtonRectangle);

            string monthText = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName (control.DisplayMonth.Month);
            if (!string.IsNullOrEmpty (monthText))
                monthText = char.ToUpper (monthText[0], CultureInfo.CurrentCulture) + monthText.Substring (1);

            string yearText = control.DisplayMonth.Year.ToString (CultureInfo.CurrentCulture);

            if (control.HoveredRectangle == control.MonthTitleRectangle)
                canvas.DrawRect (ToSKRect (control.MonthTitleRectangle), hoverPaint);

            if (control.HoveredRectangle == control.YearTitleRectangle)
                canvas.DrawRect (ToSKRect (control.YearTitleRectangle), hoverPaint);

            using var monthPaint = CreateTextPaintFrom (textPaint, monthText);
            using var yearPaint = CreateTextPaintFrom (textPaint, yearText);

            DrawCenteredText (canvas, monthText, control.MonthTitleRectangle, monthPaint);
            DrawCenteredText (canvas, yearText, control.YearTitleRectangle, yearPaint);
        }

        private static void DrawDayHeaders (DateTimePickerCalendar control, SKCanvas canvas, TextPaintResources textPaint)
        {
            var names = GetDayNames();
            int y = control.MonthTitleRectangle.Bottom;

            int firstDay = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

            for (int i = 0; i < 7; i++) {
                int index = (firstDay + i) % 7;
                var rect = new Rectangle (8 + i * 30, y, 30, 20);
                DrawCenteredText (canvas, names[index], rect, textPaint);
            }
        }

        private static string[] GetDayNames ()
        {
            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "pl")
                return new[] { "nd", "pn", "wt", "śr", "cz", "pt", "sb" };

            return CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        }

        private static void DrawDayCells (
            DateTimePickerCalendar control,
            SKCanvas canvas,
            TextPaintResources textPaint,
            TextPaintResources mutedTextPaint,
            SKPaint accentPaint,
            SKPaint hoverPaint,
            SKPaint borderPaint,
            SKPaint todayOutlinePaint)
        {
            var firstVisible = control.GetFirstVisibleDate ();
            var today = DateTime.Today;

            for (int i = 0; i < control.DayCellRectangles.Length; i++) {
                var rect = control.DayCellRectangles[i];
                var date = firstVisible.AddDays (i);

                bool isCurrentMonth = date.Month == control.DisplayMonth.Month && date.Year == control.DisplayMonth.Year;
                bool isSelected = date.Date == control.Value.Date;
                bool isToday = date.Date == today;
                bool isEnabled = date >= control.MinDate.Date && date <= control.MaxDate.Date;
                bool isHovered = control.HoveredRectangle == rect;

                if (isHovered && !isSelected)
                    canvas.DrawRect (ToSKRect (rect), hoverPaint);

                if (isSelected)
                    canvas.DrawRect (ToSKRect (rect), accentPaint);

                canvas.DrawRect (ToSKRect (rect), borderPaint);

                if (isToday && !isSelected)
                    canvas.DrawRect (ToSKRect (rect).Deflate (2, 2), todayOutlinePaint);

                var basePaint = isCurrentMonth ? textPaint : mutedTextPaint;
                using var cellPaint = CreateTextPaintFrom (basePaint, date.Day.ToString (CultureInfo.CurrentCulture));

                if (!isEnabled)
                    cellPaint.Paint.Color = cellPaint.Paint.Color.WithAlpha (90);
                else if (isSelected)
                    cellPaint.Paint.Color = SKColors.White;

                DrawCenteredText (canvas, date.Day.ToString (CultureInfo.CurrentCulture), rect, cellPaint);
            }
        }

        private static void DrawMonthCells (DateTimePickerCalendar control, SKCanvas canvas, TextPaintResources textPaint, SKPaint hoverPaint, SKPaint borderPaint, SKPaint accentPaint)
        {
            var dtf = CultureInfo.CurrentCulture.DateTimeFormat;

            for (int i = 0; i < control.MonthCellRectangles.Length; i++) {
                var rect = control.MonthCellRectangles[i];
                int month = i + 1;
                bool isSelected = month == control.Value.Month && control.DisplayMonth.Year == control.Value.Year;
                bool isHovered = control.HoveredRectangle == rect;

                if (isHovered && !isSelected)
                    canvas.DrawRect (ToSKRect (rect), hoverPaint);

                if (isSelected)
                    canvas.DrawRect (ToSKRect (rect), accentPaint);

                canvas.DrawRect (ToSKRect (rect), borderPaint);

                string text = dtf.AbbreviatedMonthNames[i];
                using var paint = CreateTextPaintFrom (textPaint, text);
                if (isSelected)
                    paint.Paint.Color = SKColors.White;

                DrawCenteredText (canvas, text, rect, paint);
            }
        }

        private static void DrawYearCells (DateTimePickerCalendar control, SKCanvas canvas, TextPaintResources textPaint, SKPaint hoverPaint, SKPaint borderPaint, SKPaint accentPaint)
        {
            for (int i = 0; i < control.YearCellRectangles.Length; i++) {
                var rect = control.YearCellRectangles[i];
                int year = control.YearRangeStart + i;
                bool isSelected = year == control.Value.Year;
                bool isHovered = control.HoveredRectangle == rect;

                if (isHovered && !isSelected)
                    canvas.DrawRect (ToSKRect (rect), hoverPaint);

                if (isSelected)
                    canvas.DrawRect (ToSKRect (rect), accentPaint);

                canvas.DrawRect (ToSKRect (rect), borderPaint);

                string text = year.ToString (CultureInfo.CurrentCulture);
                using var paint = CreateTextPaintFrom (textPaint, text);
                if (isSelected)
                    paint.Paint.Color = SKColors.White;

                DrawCenteredText (canvas, text, rect, paint);
            }
        }

        private static void DrawTodayButton (DateTimePickerCalendar control, SKCanvas canvas, TextPaintResources textPaint, SKPaint hoverPaint, SKPaint borderPaint)
        {
            bool hovered = control.HoveredRectangle == control.TodayButtonRectangle;

            if (hovered)
                canvas.DrawRect (ToSKRect (control.TodayButtonRectangle), hoverPaint);

            canvas.DrawRect (ToSKRect (control.TodayButtonRectangle), borderPaint);

            const string text = "Dzisiaj";
            using var paint = CreateTextPaintFrom (textPaint, text);
            DrawCenteredText (canvas, text, control.TodayButtonRectangle, paint);
        }

        private static void DrawButton (SKCanvas canvas, string text, Rectangle rect, TextPaintResources textPaint, SKPaint hoverPaint, bool hovered)
        {
            if (hovered)
                canvas.DrawRect (ToSKRect (rect), hoverPaint);

            using var paint = CreateTextPaintFrom (textPaint, text);
            DrawCenteredText (canvas, text, rect, paint);
        }

        private static TextPaintResources CreateTextPaint (DateTimePickerCalendar control, string sampleText)
        {
            var paint = new SKPaint {
                IsAntialias = true,
                Color = control.CurrentStyle.ForegroundColor ?? SKColors.Black
            };
            var font = new SKFont (
                ResolveTypeface (control.CurrentStyle.Font ?? Theme.UIFont, sampleText),
                control.CurrentStyle.FontSize ?? Theme.FontSize);
            return new TextPaintResources (paint, font);
        }

        private static TextPaintResources CreateTextPaintFrom (TextPaintResources source, string text)
        {
            var paint = new SKPaint {
                IsAntialias = source.Paint.IsAntialias,
                Color = source.Paint.Color
            };
            var font = new SKFont (
                ResolveTypeface (source.Font.Typeface, text),
                source.Font.Size);
            return new TextPaintResources (paint, font);
        }

        private static SKTypeface? ResolveTypeface (SKTypeface? preferred, string text)
        {
            if (preferred is not null && SupportsText (preferred, text))
                return preferred;

            var segoe = Theme.CreateTypefaceOrDefault(
                "Segoe UI",
                SKFontStyleWeight.Normal,
                SKFontStyleSlant.Upright);
            if (SupportsText (segoe, text))
                return segoe;

            return Theme.UIFont;
        }

        private static bool SupportsText (SKTypeface typeface, string text)
        {
            foreach (char c in text) {
                if (char.IsWhiteSpace (c) || char.IsPunctuation (c) || char.IsDigit (c))
                    continue;

                if (typeface.GetGlyphs (c.ToString ())[0] == 0)
                    return false;
            }

            return true;
        }

        private static void DrawCenteredText (SKCanvas canvas, string text, Rectangle rect, TextPaintResources textPaint)
        {
            var metrics = textPaint.Font.Metrics;
            float x = rect.Left + (rect.Width - textPaint.Font.MeasureText (text)) / 2f;
            float y = rect.Top + ((rect.Height - (metrics.Descent - metrics.Ascent)) / 2f) - metrics.Ascent;
            canvas.DrawText (text, x, y, SKTextAlign.Left, textPaint.Font, textPaint.Paint);
        }

        private sealed class TextPaintResources : IDisposable
        {
            public TextPaintResources (SKPaint paint, SKFont font)
            {
                Paint = paint;
                Font = font;
            }

            public SKPaint Paint { get; }

            public SKFont Font { get; }

            public void Dispose ()
            {
                Font.Dispose ();
                Paint.Dispose ();
            }
        }

        private static SKRect ToSKRect (Rectangle rect)
            => new (rect.Left, rect.Top, rect.Right, rect.Bottom);

        private static SKColor Blend (SKColor from, SKColor to, float amount)
        {
            amount = Math.Max (0f, Math.Min (1f, amount));

            byte r = (byte)(from.Red + ((to.Red - from.Red) * amount));
            byte g = (byte)(from.Green + ((to.Green - from.Green) * amount));
            byte b = (byte)(from.Blue + ((to.Blue - from.Blue) * amount));
            byte a = (byte)(from.Alpha + ((to.Alpha - from.Alpha) * amount));

            return new SKColor (r, g, b, a);
        }
    }

    internal static class SkRectExtensions
    {
        public static SKRect Deflate (this SKRect rect, float dx, float dy)
            => new (rect.Left + dx, rect.Top + dy, rect.Right - dx, rect.Bottom - dy);
    }
}
