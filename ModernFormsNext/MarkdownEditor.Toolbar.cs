using System;
using System.Collections.Generic;

namespace ModernFormsNext;

public partial class MarkdownEditor
{
    private const int MaximumToolbarAnalysisLength = 4096;

    internal ToolBar CommandToolbar => toolbar;

    private void InitializeToolbar()
    {
        AddToolbarItem(MarkdownToolbarCommand.Undo, "Undo", "Undo (Ctrl+Z)", Undo, isEditingCommand: false);
        AddToolbarItem(MarkdownToolbarCommand.Redo, "Redo", "Redo (Ctrl+Y)", Redo, isEditingCommand: false);
        AddToolbarSeparator();

        AddToolbarItem(MarkdownToolbarCommand.Bold, "B", "Bold (Ctrl+B)", ToggleBold);
        AddToolbarItem(MarkdownToolbarCommand.Italic, "I", "Italic (Ctrl+I)", ToggleItalic);
        AddToolbarItem(MarkdownToolbarCommand.Strikethrough, "S", "Strikethrough (Ctrl+Shift+X)", ToggleStrikethrough);
        AddToolbarItem(MarkdownToolbarCommand.InlineCode, "`", "Inline code (Ctrl+`)", ToggleInlineCode);
        AddToolbarSeparator();

        AddToolbarItem(MarkdownToolbarCommand.Heading, "H", "Heading level 2", () => InsertHeading(2));
        AddToolbarItem(MarkdownToolbarCommand.Quote, ">", "Block quote", ToggleBlockQuote);
        AddToolbarSeparator();

        AddToolbarItem(MarkdownToolbarCommand.UnorderedList, "-", "Unordered list (Ctrl+Shift+8)", ToggleUnorderedList);
        AddToolbarItem(MarkdownToolbarCommand.OrderedList, "1.", "Ordered list (Ctrl+Shift+7)", ToggleOrderedList);
        AddToolbarItem(MarkdownToolbarCommand.TaskList, "[ ]", "Task list", ToggleTaskList);
        AddToolbarSeparator();

        AddToolbarItem(MarkdownToolbarCommand.Link, "Link", "Insert or edit link (Ctrl+K)", RequestInsertLink);
        AddToolbarItem(MarkdownToolbarCommand.Image, "Image", "Insert or edit image", RequestInsertImage);
        AddToolbarSeparator();

        AddToolbarItem(MarkdownToolbarCommand.CodeBlock, "{ }", "Fenced code block", ToggleCodeBlock);
        AddToolbarItem(MarkdownToolbarCommand.HorizontalRule, "---", "Horizontal rule", InsertHorizontalRule);
    }

    private MenuItem AddToolbarItem(
        MarkdownToolbarCommand key,
        string text,
        string toolTipText,
        Action command,
        bool isEditingCommand = true)
    {
        var item = toolbar.Items.Add(text, onClick: (_, _) =>
        {
            command();
            if (ViewMode != MarkdownEditorViewMode.Preview)
                editorSurface.Select();
        });
        item.Padding = new Padding(9, 3, 9, 3);
        item.ToolTipText = toolTipText;
        toolbarItems.Add(key, item);

        if (isEditingCommand)
            editingToolbarItems.Add(item);
        return item;
    }

    private void AddToolbarSeparator()
        => toolbar.Items.Add(new MenuSeparatorItem { Padding = new Padding(5, 5, 5, 5) });

    private void UpdateToolbarState()
    {
        foreach (var item in editingToolbarItems)
            item.Enabled = !ReadOnly;

        toolbarItems[MarkdownToolbarCommand.Undo].Enabled = !ReadOnly && CanUndo;
        toolbarItems[MarkdownToolbarCommand.Redo].Enabled = !ReadOnly && CanRedo;
        UpdateToolbarCheckedState();
        toolbar.Invalidate();
    }

    private void UpdateToolbarCheckedState()
    {
        SetToolbarChecked(MarkdownToolbarCommand.Bold, IsInlineStateActive("**"));
        SetToolbarChecked(MarkdownToolbarCommand.Italic, IsInlineStateActive("*"));
        SetToolbarChecked(MarkdownToolbarCommand.Strikethrough, IsInlineStateActive("~~"));
        SetToolbarChecked(MarkdownToolbarCommand.InlineCode, IsInlineStateActive("`"));

        var selection = GetSurfaceSelection();
        var line = GetLineAt(selection.Start).Text;
        var indent = CountLeadingSpaces(line);
        var rest = line.AsSpan(indent);
        var headingLength = 0;
        while (headingLength < rest.Length && headingLength < 6 && rest[headingLength] == '#')
            headingLength++;

        SetToolbarChecked(
            MarkdownToolbarCommand.Heading,
            headingLength > 0 && headingLength < rest.Length && rest[headingLength] == ' ');
        SetToolbarChecked(MarkdownToolbarCommand.Quote, rest.StartsWith("> ", StringComparison.Ordinal));
        SetToolbarChecked(MarkdownToolbarCommand.TaskList, TryGetTaskMarker(line, out _));
        SetToolbarChecked(MarkdownToolbarCommand.UnorderedList, TryGetUnorderedMarker(line, out _));
        SetToolbarChecked(MarkdownToolbarCommand.OrderedList, TryGetOrderedMarker(line, out _));
        SetToolbarChecked(
            MarkdownToolbarCommand.CodeBlock,
            rest.StartsWith("```", StringComparison.Ordinal) || rest.StartsWith("~~~", StringComparison.Ordinal));
    }

    private void SetToolbarChecked(MarkdownToolbarCommand command, bool value)
    {
        if (toolbarItems.TryGetValue(command, out var item))
            item.Checked = value;
    }

    private bool IsInlineStateActive(string marker)
    {
        var source = Markdown;
        var selection = GetSurfaceSelection();
        var selectionEnd = selection.Start + selection.Length;

        if (selection.Start >= marker.Length
            && selectionEnd + marker.Length <= source.Length
            && IsDelimiterAt(source, selection.Start - marker.Length, marker)
            && IsDelimiterAt(source, selectionEnd, marker))
        {
            return true;
        }

        var line = GetLineAt(selection.Start);
        if (line.Text.Length > MaximumToolbarAnalysisLength)
            return false;

        var localStart = selection.Start - line.Start;
        var localEnd = selectionEnd - line.Start;
        var opening = LastIndexOfDelimiter(line.Text, marker, localStart - 1);
        if (opening < 0)
            return false;

        var closing = IndexOfDelimiter(line.Text, marker, Math.Max(localEnd, opening + marker.Length));
        return closing >= localEnd;
    }

    private static int LastIndexOfDelimiter(string source, string marker, int start)
    {
        for (var index = Math.Min(start, source.Length - marker.Length); index >= 0; index--)
        {
            if (IsDelimiterAt(source, index, marker))
                return index;
        }

        return -1;
    }

    private static int IndexOfDelimiter(string source, string marker, int start)
    {
        for (var index = Math.Max(0, start); index + marker.Length <= source.Length; index++)
        {
            if (IsDelimiterAt(source, index, marker))
                return index;
        }

        return -1;
    }

    private static bool IsDelimiterAt(string source, int index, string marker)
    {
        if (index < 0 || index + marker.Length > source.Length
            || !source.AsSpan(index, marker.Length).SequenceEqual(marker))
        {
            return false;
        }

        if (marker.Length != 1 || marker[0] is not ('*' or '_'))
            return true;

        return (index == 0 || source[index - 1] != marker[0])
            && (index + 1 >= source.Length || source[index + 1] != marker[0]);
    }

    private enum MarkdownToolbarCommand
    {
        Undo,
        Redo,
        Bold,
        Italic,
        Strikethrough,
        InlineCode,
        Heading,
        Quote,
        UnorderedList,
        OrderedList,
        TaskList,
        Link,
        Image,
        CodeBlock,
        HorizontalRule
    }
}
