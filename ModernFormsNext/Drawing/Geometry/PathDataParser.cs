using System.Drawing;
using System.Globalization;
using System.Text;

namespace ModernFormsNext.Drawing;

/// <summary>One data-only path grammar shared by the runtime and Designer.</summary>
internal static class PathDataParser
{
    internal const int MaxCharacters = 262144;
    internal const int MaxSegments = 16384;
    internal const int MaxFigures = 1024;

    internal static PathGeometry Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxCharacters) throw new FormatException("Path data exceeds the character limit.");
        return new Reader(data).Read();
    }

    private ref struct Reader(ReadOnlySpan<char> text)
    {
        private readonly ReadOnlySpan<char> text = text;
        private int position;
        private int segments;
        private PathGeometry geometry = new();
        private PathFigure? figure;
        private PointF current;
        private PointF cubicControl;
        private PointF quadraticControl;
        private char previous;

        internal PathGeometry Read()
        {
            char command = '\0';
            while (true)
            {
                White();
                if (position == text.Length) return geometry;
                if (char.IsAsciiLetter(text[position]))
                {
                    command = text[position++];
                    White();
                    if (position < text.Length && text[position] == ',') Fail("A command cannot begin with a comma.");
                }
                else if (command == '\0') Fail("A path command is required.");
                char upper = char.ToUpperInvariant(command);
                bool relative = char.IsLower(command);
                if (figure is null && upper != 'M') Fail("A path must begin with a move command.");
                if (upper == 'Z')
                {
                    figure!.IsClosed = true;
                    current = figure.StartPoint;
                    previous = 'Z';
                    command = '\0';
                    continue;
                }
                if (upper == 'M')
                {
                    PointF point = Point(relative);
                    NewFigure(point);
                    current = point;
                    previous = 'M';
                    command = relative ? 'l' : 'L';
                    continue;
                }
                if (figure!.IsClosed) NewFigure(current);
                PathSegment segment;
                switch (upper)
                {
                    case 'L': segment = new LineSegment(Point(relative)); break;
                    case 'H': segment = new LineSegment(new PointF(Coordinate(Number(), current.X, relative), current.Y)); break;
                    case 'V': segment = new LineSegment(new PointF(current.X, Coordinate(Number(), current.Y, relative))); break;
                    case 'C':
                        PointF c1 = Point(relative), c2 = Point(relative), end = Point(relative);
                        segment = new BezierSegment(c1, c2, end); cubicControl = c2; break;
                    case 'S':
                        PointF reflected = previous is 'C' or 'S' ? Reflect(cubicControl) : current;
                        PointF smoothControl = Point(relative), smoothEnd = Point(relative);
                        segment = new BezierSegment(reflected, smoothControl, smoothEnd); cubicControl = smoothControl; break;
                    case 'Q':
                        PointF control = Point(relative), quadraticEnd = Point(relative);
                        segment = new QuadraticBezierSegment(control, quadraticEnd); quadraticControl = control; break;
                    case 'T':
                        PointF reflectedQuadratic = previous is 'Q' or 'T' ? Reflect(quadraticControl) : current;
                        segment = new QuadraticBezierSegment(reflectedQuadratic, Point(relative)); quadraticControl = reflectedQuadratic; break;
                    case 'A':
                        float rx = Math.Abs(Number()), ry = Math.Abs(Number()), angle = Number();
                        bool large = Flag(), clockwise = Flag();
                        segment = new ArcSegment(Point(relative), new SizeF(rx, ry), angle, large,
                            clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise); break;
                    default: throw Error("The path command is not supported.");
                }
                if (++segments > MaxSegments) Fail("Path data exceeds the segment limit.");
                figure.Segments.Add(segment);
                current = segment switch
                {
                    LineSegment line => line.Point,
                    BezierSegment cubic => cubic.Point,
                    QuadraticBezierSegment quadratic => quadratic.Point,
                    ArcSegment arc => arc.Point,
                    _ => current
                };
                previous = upper;
            }
        }

        private void NewFigure(PointF point)
        {
            if (geometry.Figures.Count >= MaxFigures) Fail("Path data exceeds the figure limit.");
            figure = new PathFigure { StartPoint = point };
            geometry.Figures.Add(figure);
        }
        private PointF Point(bool relative) => new(Coordinate(Number(), current.X, relative), Coordinate(Number(), current.Y, relative));
        private PointF Reflect(PointF point) => new(Finite(2d * current.X - point.X), Finite(2d * current.Y - point.Y));
        private static float Coordinate(float value, float origin, bool relative) => relative ? Finite((double)value + origin) : value;
        private static float Finite(double value)
        {
            float result = (float)value;
            if (!float.IsFinite(result)) throw new FormatException("A path coordinate is outside the finite range.");
            return result;
        }
        private bool Flag()
        {
            Separator();
            if (position >= text.Length || text[position] is not ('0' or '1')) Fail("An arc flag must be 0 or 1.");
            return text[position++] == '1';
        }
        private float Number()
        {
            Separator();
            int start = position;
            if (position < text.Length && text[position] is '+' or '-') position++;
            int digits = Digits();
            if (position < text.Length && text[position] == '.') { position++; digits += Digits(); }
            if (digits == 0) Fail("A numeric path parameter is required.");
            if (position < text.Length && text[position] is 'e' or 'E')
            {
                position++;
                if (position < text.Length && text[position] is '+' or '-') position++;
                if (Digits() == 0) Fail("A path exponent requires digits.");
            }
            if (position - start > 64) Fail("A path number is too long.");
            if (!float.TryParse(text[start..position], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value)) Fail("A path number is outside the finite range.");
            return value;
        }
        private int Digits()
        {
            int start = position;
            while (position < text.Length && char.IsAsciiDigit(text[position])) position++;
            return position - start;
        }
        private void Separator()
        {
            White();
            if (position < text.Length && text[position] == ',') { position++; White(); }
        }
        private void White()
        {
            while (position < text.Length && text[position] is ' ' or '\t' or '\r' or '\n') position++;
        }
        private FormatException Error(string message) => new($"{message} (offset {position}).");
        private void Fail(string message) => throw Error(message);
    }

    internal static string Write(PathGeometry geometry)
    {
        var result = new StringBuilder();
        void Number(float value) => result.Append(' ').Append(value.ToString("R", CultureInfo.InvariantCulture));
        void Point(PointF value) { Number(value.X); Number(value.Y); }
        foreach (PathFigure figure in geometry.Figures)
        {
            if (result.Length > 0) result.Append(' ');
            result.Append('M'); Point(figure.StartPoint);
            foreach (PathSegment segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line: result.Append(" L"); Point(line.Point); break;
                    case QuadraticBezierSegment quadratic: result.Append(" Q"); Point(quadratic.ControlPoint); Point(quadratic.Point); break;
                    case BezierSegment cubic: result.Append(" C"); Point(cubic.ControlPoint1); Point(cubic.ControlPoint2); Point(cubic.Point); break;
                    case ArcSegment arc:
                        result.Append(" A"); Number(arc.Size.Width); Number(arc.Size.Height); Number(arc.RotationAngle);
                        result.Append(arc.IsLargeArc ? " 1" : " 0").Append(arc.SweepDirection == SweepDirection.Clockwise ? " 1" : " 0"); Point(arc.Point); break;
                    default: throw new NotSupportedException("Custom segments cannot be written as path data.");
                }
            }
            if (figure.IsClosed) result.Append(" Z");
        }
        return result.ToString();
    }
}
