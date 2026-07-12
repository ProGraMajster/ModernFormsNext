using ModernFormsNext.Documents;
using Xunit;

namespace ModernFormsNext.Tests;

public class MarkdownParserTableTests
{
    [Fact]
    public void PipeTableCreatesSemanticTableBlockWithAlignment()
    {
        var document = new MarkdownParser().Parse("""
            | Name | Count | State |
            | :--- | ----: | :---: |
            | Button | 10 | stable |
            """);

        var table = Assert.IsType<TableBlock>(Assert.Single(document.Blocks));

        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(DocumentTextAlignment.Left, table.Columns[0].Alignment);
        Assert.Equal(DocumentTextAlignment.Right, table.Columns[1].Alignment);
        Assert.Equal(DocumentTextAlignment.Center, table.Columns[2].Alignment);

        Assert.Equal(2, table.Rows.Count);
        Assert.True(table.Rows[0].IsHeader);
        Assert.False(table.Rows[1].IsHeader);
        Assert.Equal("Name", new Document(new[] { table.Rows[0].Cells[0].Blocks.Single() }).GetPlainText());
        Assert.Equal("10", new Document(new[] { table.Rows[1].Cells[1].Blocks.Single() }).GetPlainText());
    }

    [Fact]
    public void TableLayoutUsesNativeCellElementsAndWrapsToAvailableWidth()
    {
        var document = new MarkdownParser().Parse("""
            | Name | Description |
            | --- | --- |
            | Button | A long description that should wrap inside the cell instead of creating a horizontal scrollbar. |
            """);

        var layout = DocumentTestHelpers.LayoutDocument(document, 180);
        var cells = layout.Elements.OfType<DocumentTableCellLayoutElement>().ToArray();

        Assert.Equal(4, cells.Length);
        Assert.Contains(cells, cell => cell.Bounds.Width > 90);
        Assert.Contains(cells, cell => cell.Bounds.Width < 90);
        Assert.All(cells, cell => Assert.True(cell.BorderThickness > 0));
        Assert.Contains(layout.Elements.OfType<DocumentTextLayoutElement>(), element => element.Text.Contains("long description"));
    }
}
