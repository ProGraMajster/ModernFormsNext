using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Automation.Text;

internal static class TextScenario
{
    internal static int Run(string handle)
    {
        if (!long.TryParse(handle, out long value) || value == 0) return 2;
        try {
            var root = AutomationElement.FromHandle(new IntPtr(value));
            AutomationElement Find(string id) => root.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, id)) ?? throw new InvalidOperationException("Missing fixture element.");
            void Invoke(string id) => ((InvokePattern)Find(id).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            var editor = Find("uia.text.editor");
            var readOnly = Find("uia.text.readonly");
            var focus = Find("uia.text.focus");
            focus.SetFocus();
            var pattern = (TextPattern)editor.GetCurrentPattern(TextPattern.Pattern);
            var document = pattern.DocumentRange;
            string expected = "alpha 😀 beta\n" + string.Join("\n", Enumerable.Range(0, 40).Select(i => "line " + i)) + "\nomega";
            bool fullText = document.GetText(-1) == expected;
            var alpha = document.FindText("alpha", false, false)!;
            var beta = document.FindText("beta", false, false)!;
            var tail = document.FindText("omega", false, false)!;
            bool cloneMatches = alpha.Compare(alpha.Clone());
            bool endpointOrder = alpha.CompareEndpoints(TextPatternRangeEndpoint.End, beta, TextPatternRangeEndpoint.Start) < 0;
            bool enclosingElement = alpha.GetEnclosingElement().GetRuntimeId().SequenceEqual(editor.GetRuntimeId());
            bool noEmbeddedObjects = document.GetChildren().Length == 0;
            bool mixedFormatting = ReferenceEquals(document.GetAttributeValue(TextPattern.ForegroundColorAttribute), TextPattern.MixedAttributeValue);
            var blue = document.FindAttribute(TextPattern.ForegroundColorAttribute, 0x00ff0000, false);
            bool attributeSearch = blue?.GetText(-1) == "alpha";
            var firstRectangles = alpha.GetBoundingRectangles();
            bool visibleGeometry = firstRectangles.Length > 0 && firstRectangles.All(r => r.Width > 0 && r.Height > 0);
            bool pointRange = false;
            if (firstRectangles.Length > 0) {
                var rect = firstRectangles[0];
                var point = pattern.RangeFromPoint(new System.Windows.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
                pointRange = point.GetText(-1) == "" && point.CompareEndpoints(TextPatternRangeEndpoint.Start, alpha, TextPatternRangeEndpoint.Start) >= 0
                    && point.CompareEndpoints(TextPatternRangeEndpoint.End, alpha, TextPatternRangeEndpoint.End) <= 0;
            }
            alpha.Select();
            bool selection = pattern.GetSelection() is { Length: 1 } selected && selected[0].GetText(-1) == "alpha";
            // Independent range movement must not edit text or replace the selected alpha span.
            var cursor = alpha.Clone();
            cursor.MoveEndpointByRange(TextPatternRangeEndpoint.End, cursor, TextPatternRangeEndpoint.Start);
            int moved = cursor.Move(TextUnit.Character, 7);
            cursor.ExpandToEnclosingUnit(TextUnit.Character);
            bool characterNavigation = moved == 7 && cursor.GetText(-1) == " "; // emoji is one character at index 6.
            var word = beta.Clone();
            word.ExpandToEnclosingUnit(TextUnit.Word);
            bool wordNavigation = word.GetText(-1).Contains("beta", StringComparison.Ordinal);
            bool initiallyScrolledOut = tail.GetBoundingRectangles().Length == 0;
            tail.ScrollIntoView(false);
            bool scrollRevealed = tail.GetBoundingRectangles().Length > 0;
            bool selectionPreserved = pattern.GetSelection()[0].GetText(-1) == "alpha";
            bool focusPreserved = focus.Current.HasKeyboardFocus;
            bool visibleRanges = pattern.GetVisibleRanges().Length > 0;
            bool readDidNotEdit = document.GetText(-1) == expected;
            Invoke("uia.text.append");
            bool retainedRangeRebased = document.GetText(-1) == expected + " tail";
            var readOnlyPattern = (TextPattern)readOnly.GetCurrentPattern(TextPattern.Pattern);
            var readOnlyRange = readOnlyPattern.DocumentRange;
            bool readOnlyAttribute = Equals(readOnlyRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute), true);
            readOnlyRange.FindText("only", false, false)!.Select();
            bool readOnlySelection = readOnlyPattern.GetSelection()[0].GetText(-1) == "only";
            Invoke("uia.text.protect");
            bool passwordFlag = editor.Current.IsPassword;
            bool freshTextUnavailable = !editor.TryGetCurrentPattern(TextPattern.Pattern, out _);
            bool retainedTextDenied = Denied(() => document.GetText(-1));
            bool retainedGeometryDenied = Denied(() => document.GetBoundingRectangles());
            Invoke("uia.text.remove");
            bool removedRangeDenied = Denied(() => readOnlyRange.GetText(-1));
            Console.WriteLine(JsonSerializer.Serialize(new {
                FullText = fullText, CloneMatches = cloneMatches, EndpointOrder = endpointOrder,
                EnclosingElement = enclosingElement, NoEmbeddedObjects = noEmbeddedObjects,
                MixedFormatting = mixedFormatting, AttributeSearch = attributeSearch,
                VisibleGeometry = visibleGeometry, PointRange = pointRange, Selection = selection,
                CharacterNavigation = characterNavigation, WordNavigation = wordNavigation,
                InitiallyScrolledOut = initiallyScrolledOut, ScrollRevealed = scrollRevealed,
                SelectionPreserved = selectionPreserved, FocusPreserved = focusPreserved,
                VisibleRanges = visibleRanges, ReadDidNotEdit = readDidNotEdit,
                RetainedRangeRebased = retainedRangeRebased, ReadOnlyAttribute = readOnlyAttribute,
                ReadOnlySelection = readOnlySelection, PasswordFlag = passwordFlag,
                FreshTextUnavailable = freshTextUnavailable, RetainedTextDenied = retainedTextDenied,
                RetainedGeometryDenied = retainedGeometryDenied, RemovedRangeDenied = removedRangeDenied }));
            return 0;
        }
        catch (Exception error) {
            // Logs identify a failed native call without echoing document content or OS messages.
            Console.Error.WriteLine($"Native text client: {error.GetType().Name} (0x{error.HResult:X8}).");
            return 1;
        }
    }

    private static bool Denied(Func<object> read)
    {
        try { _ = read(); return false; }
        catch (Exception error) when (error is UnauthorizedAccessException or ElementNotAvailableException or InvalidOperationException or COMException) { return true; }
    }
}
