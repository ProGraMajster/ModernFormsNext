using System;
using System.IO;
using ModernFormsNext.VisualStudioExtension;
using ModernFormsNext.VisualStudioExtension.Editors;
using Xunit;

namespace ModernFormsNext.VisualStudioExtension.Vsix.Tests;

public sealed class DesignerCodeDocumentTests
{
    private static string DesignPath => Path.Combine(Path.GetTempPath(), "ModernFormsNext-code-buffer", "Sample.mfdesign");

    [Fact]
    public void ReadsUnsavedTextAndInsertsWithoutSavingOrReplacingTheRest()
    {
        var buffer = new Buffer { Text = "// unsaved user text\r\n" };
        var read = Request("READ");
        Assert.True(Result(read, buffer, out var source));
        Assert.Equal(buffer.Text, source);
        var insertion = Request("INSERT", buffer.Text, 3, "new ");
        Assert.True(Result(insertion, buffer, out _));
        Assert.Equal("// new unsaved user text\r\n", buffer.Text);
        Assert.Equal(1, buffer.EditCount);
        Assert.Equal(1, buffer.QueryCount);
    }

    [Fact]
    public void StaleSnapshotAndDeniedQueryEditNeverWrite()
    {
        var buffer = new Buffer { Text = "// newer text" };
        Assert.False(Result(Request("INSERT", "// old text", 0, "// handler"), buffer, out _));
        Assert.Equal(0, buffer.QueryCount);
        buffer.AllowEdit = false;
        Assert.False(Result(Request("INSERT", buffer.Text, 0, "// handler"), buffer, out _));
        Assert.Equal(1, buffer.QueryCount);
        Assert.Equal(0, buffer.EditCount);
        Assert.Equal("// newer text", buffer.Text);
    }

    [Fact]
    public void QueryEditReentrancyRechecksTheCurrentTextAndDocumentIdentity()
    {
        var buffer = new Buffer { Text = "// current text" };
        buffer.OnQueryEdit = () => buffer.Text = "// changed during QueryEdit";
        Assert.False(Result(Request("INSERT", buffer.Text, 0, "// handler"), buffer, out _));
        Assert.Equal("// changed during QueryEdit", buffer.Text);
        Assert.Equal(0, buffer.EditCount);
        var path = DesignPath;
        buffer.OnQueryEdit = () => path = Path.ChangeExtension(DesignPath, ".other");
        var request = Request("INSERT", buffer.Text, 0, "// handler");
        var response = VisualStudioCodeDocumentService.Execute(request, () => path, _ => buffer);
        Assert.True(DesignerCodeRequest.TryReadResult(response, request.Id, out var succeeded, out _));
        Assert.False(succeeded);
        Assert.Equal(0, buffer.EditCount);
    }

    [Fact]
    public void ReopensTheBufferAfterQueryEditInsteadOfEditingAClosedBuffer()
    {
        var original = new Buffer { Text = "// original" };
        var replacement = new Buffer { Text = "// newly opened user text" };
        var current = original;
        original.OnQueryEdit = () => current = replacement;
        var request = Request("INSERT", original.Text, 0, "// handler");
        var response = VisualStudioCodeDocumentService.Execute(request, () => DesignPath, _ => current);
        Assert.True(DesignerCodeRequest.TryReadResult(response, request.Id, out var succeeded, out _));
        Assert.False(succeeded);
        Assert.Equal(0, original.EditCount);
        Assert.Equal(0, replacement.EditCount);
    }

    [Fact]
    public void NavigatesOnlyAtTheExpectedCurrentUtf16Location()
    {
        var buffer = new Buffer { Text = "// żółć\r\n" };
        Assert.True(Result(Request("NAVIGATE", buffer.Text, 5), buffer, out _));
        Assert.Equal(5, buffer.Caret);
        Assert.False(Result(Request("NAVIGATE", "// old text", 1), buffer, out _));
        Assert.Equal(5, buffer.Caret);
        Assert.Equal(0, buffer.EditCount);
    }

    [Fact]
    public void ForeignPathsAndOversizedBuffersAreRejectedBeforeEditing()
    {
        var buffer = new Buffer { Text = new string(' ', DesignerCodeRequest.MaximumTextCharacters + 1) };
        Assert.False(Result(Request("READ"), buffer, out _));
        var request = Request("READ");
        var opened = false;
        var response = VisualStudioCodeDocumentService.Execute(request, () => Path.ChangeExtension(DesignPath, ".other"),
            _ => { opened = true; return buffer; });
        Assert.False(opened);
        Assert.True(DesignerCodeRequest.TryReadResult(response, request.Id, out var succeeded, out _));
        Assert.False(succeeded);
    }

    private static bool Result(DesignerCodeRequest request, Buffer buffer, out string source)
    {
        var response = VisualStudioCodeDocumentService.Execute(request, () => DesignPath, _ => buffer);
        Assert.True(DesignerCodeRequest.TryReadResult(response, request.Id, out var success, out source));
        return success;
    }

    private static DesignerCodeRequest Request(string operation, string text = "", int offset = 0, string insertion = "")
        => new(Guid.NewGuid().ToString("N"), operation, DesignPath, Path.ChangeExtension(DesignPath, ".cs"),
            text, offset, insertion, DateTime.UtcNow.AddSeconds(30).Ticks);

    private sealed class Buffer : IDesignerCodeBuffer
    {
        internal string Text = string.Empty;
        internal bool AllowEdit = true;
        internal int EditCount;
        internal int QueryCount;
        internal int Caret = -1;
        internal Action? OnQueryEdit;
        public string ReadText() => Text;
        public bool QueryEdit() { QueryCount++; OnQueryEdit?.Invoke(); return AllowEdit; }
        public bool Insert(int offset, string text) { Text = Text.Insert(offset, text); EditCount++; return true; }
        public bool Navigate(string expectedText, int offset, Func<bool> isCurrent)
        { if (!isCurrent() || Text != expectedText) return false; Caret = offset; return true; }
    }
}
