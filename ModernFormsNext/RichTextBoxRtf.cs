using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Converts between the portable RichTextBox text model and a small RTF subset.
    /// </summary>
    /// <remarks>
    /// This is deliberately not a native RichEdit clone. It preserves text, common font style
    /// flags, foreground color, background color, and font size/family so ModernFormsNext can
    /// load and save useful RTF without pulling platform-specific code into the shared control.
    /// Unsupported RTF destinations are skipped as plain compatibility data.
    /// </remarks>
    internal static class RichTextBoxRtf
    {
        private static readonly Regex ColorRegex = new Regex(@"\\red(?<r>\d+)\\green(?<g>\d+)\\blue(?<b>\d+);", RegexOptions.Compiled);
        private static readonly Regex FontRegex = new Regex(@"\\f(?<index>\d+)[^;{}]*?\s(?<name>[^;{}]+);", RegexOptions.Compiled);

        public static string Create(string text, IReadOnlyList<RichTextBoxTextRun> runs, RichTextBoxTextStyle defaultStyle)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(runs);
            ArgumentNullException.ThrowIfNull(defaultStyle);

            var fontMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var colorMap = new Dictionary<SKColor, int>();

            AddFont(fontMap, ResolveFont(defaultStyle));
            AddColor(colorMap, ResolveForeColor(defaultStyle));

            foreach (var run in runs) {
                AddFont(fontMap, ResolveFont(run.Style, defaultStyle));
                AddColor(colorMap, ResolveForeColor(run.Style, defaultStyle));

                if (run.Style.BackColor.HasValue && run.Style.BackColor.Value != SKColor.Empty)
                    AddColor(colorMap, run.Style.BackColor.Value);
            }

            var builder = new StringBuilder();
            builder.Append(@"{\rtf1\ansi\deff0");
            AppendFontTable(builder, fontMap);
            AppendColorTable(builder, colorMap);
            builder.Append(@"\viewkind4\uc1\pard");

            foreach (var run in NormalizeRuns(text, runs, defaultStyle)) {
                AppendStyle(builder, run.Style, defaultStyle, fontMap, colorMap);
                AppendEscapedText(builder, text.Substring(run.Start, run.Length));
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static (string Text, List<RichTextBoxTextRun> Runs) Parse(string value, RichTextBoxTextStyle defaultStyle)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(defaultStyle);

            if (!LooksLikeRtf(value)) {
                return (value, value.Length == 0
                    ? new List<RichTextBoxTextRun>()
                    : new List<RichTextBoxTextRun> { new RichTextBoxTextRun(0, value.Length, new RichTextBoxTextStyle()) });
            }

            var colors = ParseColorTable(value);
            var fonts = ParseFontTable(value);
            var text = new StringBuilder();
            var runs = new List<RichTextBoxTextRun>();
            var state = defaultStyle.Clone();
            var stack = new Stack<RichTextBoxTextStyle>();

            for (var i = 0; i < value.Length; i++) {
                var c = value[i];

                if (c == '{') {
                    if (IsDestinationGroup(value, i, out var end)) {
                        i = end;
                        continue;
                    }

                    stack.Push(state.Clone());
                    continue;
                }

                if (c == '}') {
                    if (stack.Count > 0)
                        state = stack.Pop();
                    continue;
                }

                if (c != '\\') {
                    AppendParsedText(text, runs, c.ToString(), state);
                    continue;
                }

                if (i + 1 >= value.Length)
                    break;

                var next = value[++i];
                if (next is '\\' or '{' or '}') {
                    AppendParsedText(text, runs, next.ToString(), state);
                    continue;
                }

                if (!char.IsLetter(next)) {
                    if (next == '~')
                        AppendParsedText(text, runs, "\u00A0", state);
                    else if (next == '_')
                        AppendParsedText(text, runs, "-", state);
                    continue;
                }

                var wordStart = i;
                while (i < value.Length && char.IsLetter(value[i]))
                    i++;

                var word = value.Substring(wordStart, i - wordStart);
                var sign = 1;
                if (i < value.Length && value[i] == '-') {
                    sign = -1;
                    i++;
                }

                var numberStart = i;
                while (i < value.Length && char.IsDigit(value[i]))
                    i++;

                int? parameter = null;
                if (i > numberStart)
                    parameter = sign * int.Parse(value.Substring(numberStart, i - numberStart), CultureInfo.InvariantCulture);

                if (i < value.Length && value[i] != ' ')
                    i--;

                ApplyControlWord(word, parameter, ref state, defaultStyle, colors, fonts, text, runs, value, ref i);
            }

            MergeAdjacentRuns(runs);
            return (text.ToString(), runs);
        }

        private static void AddColor(Dictionary<SKColor, int> map, SKColor color)
        {
            if (color == SKColor.Empty)
                return;

            if (!map.ContainsKey(color))
                map[color] = map.Count + 1;
        }

        private static void AddFont(Dictionary<string, int> map, Font font)
        {
            if (!map.ContainsKey(font.FamilyName))
                map[font.FamilyName] = map.Count;
        }

        private static void AppendColorTable(StringBuilder builder, Dictionary<SKColor, int> colorMap)
        {
            builder.Append(@"{\colortbl ;");

            foreach (var color in colorMap.OrderBy(pair => pair.Value).Select(pair => pair.Key)) {
                builder
                    .Append(@"\red").Append(color.Red.ToString(CultureInfo.InvariantCulture))
                    .Append(@"\green").Append(color.Green.ToString(CultureInfo.InvariantCulture))
                    .Append(@"\blue").Append(color.Blue.ToString(CultureInfo.InvariantCulture))
                    .Append(';');
            }

            builder.Append('}');
        }

        private static void AppendEscapedText(StringBuilder builder, string text)
        {
            foreach (var c in text) {
                switch (c) {
                    case '\\':
                    case '{':
                    case '}':
                        builder.Append('\\').Append(c);
                        break;
                    case '\r':
                        break;
                    case '\n':
                        builder.Append(@"\par ");
                        break;
                    case '\t':
                        builder.Append(@"\tab ");
                        break;
                    default:
                        if (c <= 0x7f) {
                            builder.Append(c);
                        } else {
                            builder.Append(@"\u")
                                .Append(((short)c).ToString(CultureInfo.InvariantCulture))
                                .Append('?');
                        }

                        break;
                }
            }
        }

        private static void AppendFontTable(StringBuilder builder, Dictionary<string, int> fontMap)
        {
            builder.Append(@"{\fonttbl");

            foreach (var pair in fontMap.OrderBy(pair => pair.Value)) {
                builder
                    .Append(@"{\f").Append(pair.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(' ')
                    .Append(pair.Key.Replace(";", string.Empty))
                    .Append(";}");
            }

            builder.Append('}');
        }

        private static void AppendParsedText(StringBuilder text, List<RichTextBoxTextRun> runs, string value, RichTextBoxTextStyle style)
        {
            if (value.Length == 0)
                return;

            var start = text.Length;
            text.Append(value);
            runs.Add(new RichTextBoxTextRun(start, value.Length, style));
        }

        private static void AppendStyle(StringBuilder builder, RichTextBoxTextStyle style, RichTextBoxTextStyle defaultStyle, Dictionary<string, int> fontMap, Dictionary<SKColor, int> colorMap)
        {
            var font = ResolveFont(style, defaultStyle);
            var styleFlags = font.Style;
            var foreColor = ResolveForeColor(style, defaultStyle);

            builder
                .Append(@"\f").Append(fontMap[font.FamilyName].ToString(CultureInfo.InvariantCulture))
                .Append(@"\fs").Append(Math.Max(1, (int)Math.Round(font.SizeInPoints * 2f)).ToString(CultureInfo.InvariantCulture))
                .Append(styleFlags.HasFlag(FontStyle.Bold) ? @"\b" : @"\b0")
                .Append(styleFlags.HasFlag(FontStyle.Italic) ? @"\i" : @"\i0")
                .Append(styleFlags.HasFlag(FontStyle.Underline) ? @"\ul" : @"\ulnone")
                .Append(styleFlags.HasFlag(FontStyle.Strikeout) ? @"\strike" : @"\strike0")
                .Append(@"\cf").Append(colorMap[foreColor].ToString(CultureInfo.InvariantCulture));

            if (style.BackColor.HasValue && style.BackColor.Value != SKColor.Empty)
                builder.Append(@"\highlight").Append(colorMap[style.BackColor.Value].ToString(CultureInfo.InvariantCulture));
            else
                builder.Append(@"\highlight0");

            builder.Append(' ');
        }

        private static void ApplyControlWord(
            string word,
            int? parameter,
            ref RichTextBoxTextStyle state,
            RichTextBoxTextStyle defaultStyle,
            IReadOnlyList<SKColor?> colors,
            IReadOnlyDictionary<int, string> fonts,
            StringBuilder text,
            List<RichTextBoxTextRun> runs,
            string source,
            ref int index)
        {
            switch (word) {
                case "plain":
                    state = defaultStyle.Clone();
                    break;
                case "par":
                case "line":
                    AppendParsedText(text, runs, "\n", state);
                    break;
                case "tab":
                    AppendParsedText(text, runs, "\t", state);
                    break;
                case "b":
                    state.Font = WithStyle(state.Font, defaultStyle, FontStyle.Bold, parameter.GetValueOrDefault(1) != 0);
                    break;
                case "i":
                    state.Font = WithStyle(state.Font, defaultStyle, FontStyle.Italic, parameter.GetValueOrDefault(1) != 0);
                    break;
                case "ul":
                    state.Font = WithStyle(state.Font, defaultStyle, FontStyle.Underline, parameter.GetValueOrDefault(1) != 0);
                    break;
                case "ulnone":
                    state.Font = WithStyle(state.Font, defaultStyle, FontStyle.Underline, false);
                    break;
                case "strike":
                    state.Font = WithStyle(state.Font, defaultStyle, FontStyle.Strikeout, parameter.GetValueOrDefault(1) != 0);
                    break;
                case "fs":
                    if (parameter.HasValue && parameter.Value > 0)
                        state.Font = WithSize(state.Font, defaultStyle, parameter.Value / 2f);
                    break;
                case "f":
                    if (parameter.HasValue && fonts.TryGetValue(parameter.Value, out var fontName))
                        state.Font = WithFamily(state.Font, defaultStyle, fontName);
                    break;
                case "cf":
                    state.ForeColor = GetColor(colors, parameter);
                    break;
                case "highlight":
                    state.BackColor = GetColor(colors, parameter);
                    break;
                case "u":
                    if (parameter.HasValue) {
                        AppendParsedText(text, runs, char.ConvertFromUtf32(parameter.Value < 0 ? parameter.Value + 65536 : parameter.Value), state);

                        if (index + 1 < source.Length && source[index + 1] != '\\' && source[index + 1] != '{' && source[index + 1] != '}')
                            index++;
                    }
                    break;
            }
        }

        private static SKColor? GetColor(IReadOnlyList<SKColor?> colors, int? parameter)
        {
            if (!parameter.HasValue || parameter.Value <= 0 || parameter.Value >= colors.Count)
                return null;

            return colors[parameter.Value];
        }

        private static bool IsDestinationGroup(string value, int openBraceIndex, out int groupEnd)
        {
            groupEnd = openBraceIndex;
            var probe = openBraceIndex + 1;

            while (probe < value.Length && char.IsWhiteSpace(value[probe]))
                probe++;

            if (probe < value.Length && value[probe] == '\\') {
                probe++;
                if (probe < value.Length && value[probe] == '*') {
                    groupEnd = FindGroupEnd(value, openBraceIndex);
                    return true;
                }

                var start = probe;
                while (probe < value.Length && char.IsLetter(value[probe]))
                    probe++;

                var word = value.Substring(start, probe - start);
                if (word is "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object") {
                    groupEnd = FindGroupEnd(value, openBraceIndex);
                    return true;
                }
            }

            return false;
        }

        private static int FindGroupEnd(string value, int openBraceIndex)
        {
            var depth = 0;

            for (var i = openBraceIndex; i < value.Length; i++) {
                if (value[i] == '\\') {
                    i++;
                    continue;
                }

                if (value[i] == '{')
                    depth++;
                else if (value[i] == '}') {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return value.Length - 1;
        }

        private static bool LooksLikeRtf(string value)
            => value.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal);

        private static void MergeAdjacentRuns(List<RichTextBoxTextRun> runs)
        {
            for (var i = runs.Count - 1; i > 0; i--) {
                var current = runs[i];
                var previous = runs[i - 1];

                if (previous.End == current.Start && previous.Style.Equals(current.Style)) {
                    previous.Length += current.Length;
                    runs.RemoveAt(i);
                }
            }
        }

        private static IEnumerable<RichTextBoxTextRun> NormalizeRuns(string text, IReadOnlyList<RichTextBoxTextRun> runs, RichTextBoxTextStyle defaultStyle)
        {
            if (text.Length == 0)
                yield break;

            if (runs.Count == 0) {
                yield return new RichTextBoxTextRun(0, text.Length, defaultStyle);
                yield break;
            }

            foreach (var run in runs.OrderBy(run => run.Start)) {
                if (run.Length <= 0 || run.Start >= text.Length)
                    continue;

                var start = Math.Max(0, run.Start);
                var end = Math.Min(text.Length, run.End);
                if (end > start)
                    yield return new RichTextBoxTextRun(start, end - start, run.Style);
            }
        }

        private static List<SKColor?> ParseColorTable(string value)
        {
            var colors = new List<SKColor?> { null };
            var start = value.IndexOf(@"{\colortbl", StringComparison.Ordinal);
            if (start < 0)
                return colors;

            var end = FindGroupEnd(value, start);
            var table = value.Substring(start, end - start + 1);

            foreach (Match match in ColorRegex.Matches(table)) {
                colors.Add(new SKColor(
                    byte.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture),
                    byte.Parse(match.Groups["g"].Value, CultureInfo.InvariantCulture),
                    byte.Parse(match.Groups["b"].Value, CultureInfo.InvariantCulture)));
            }

            return colors;
        }

        private static Dictionary<int, string> ParseFontTable(string value)
        {
            var fonts = new Dictionary<int, string>();
            var start = value.IndexOf(@"{\fonttbl", StringComparison.Ordinal);
            if (start < 0)
                return fonts;

            var end = FindGroupEnd(value, start);
            var table = value.Substring(start, end - start + 1);

            foreach (Match match in FontRegex.Matches(table)) {
                fonts[int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture)] = match.Groups["name"].Value.Trim();
            }

            return fonts;
        }

        private static SKColor ResolveForeColor(RichTextBoxTextStyle style)
            => style.ForeColor ?? SKColors.Black;

        private static SKColor ResolveForeColor(RichTextBoxTextStyle style, RichTextBoxTextStyle defaultStyle)
            => style.ForeColor ?? defaultStyle.ForeColor ?? SKColors.Black;

        private static Font ResolveFont(RichTextBoxTextStyle style)
            => style.Font ?? new Font("Segoe UI", 9f);

        private static Font ResolveFont(RichTextBoxTextStyle style, RichTextBoxTextStyle defaultStyle)
            => style.Font ?? defaultStyle.Font ?? new Font("Segoe UI", 9f);

        private static Font WithFamily(Font? font, RichTextBoxTextStyle defaultStyle, string familyName)
        {
            var current = ResolveFont(font is null ? new RichTextBoxTextStyle() : new RichTextBoxTextStyle { Font = font }, defaultStyle);
            return new Font(familyName, current.SizeInPoints, current.Style);
        }

        private static Font WithSize(Font? font, RichTextBoxTextStyle defaultStyle, float size)
        {
            var current = ResolveFont(font is null ? new RichTextBoxTextStyle() : new RichTextBoxTextStyle { Font = font }, defaultStyle);
            return new Font(current.FamilyName, Math.Max(1f, size), current.Style);
        }

        private static Font WithStyle(Font? font, RichTextBoxTextStyle defaultStyle, FontStyle style, bool enabled)
        {
            var current = ResolveFont(font is null ? new RichTextBoxTextStyle() : new RichTextBoxTextStyle { Font = font }, defaultStyle);
            var newStyle = enabled ? current.Style | style : current.Style & ~style;
            return new Font(current.FamilyName, current.SizeInPoints, newStyle);
        }
    }
}
