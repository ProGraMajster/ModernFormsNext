using System.Drawing;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Backend.Android.Accessibility;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidGridAccessibilityTests
{
    [Fact]
    public void EditableGridCellUsesCanonicalStringValueAndReadOnlyDisablesMutation()
    {
        using var grid = new DataGridView { Size = new System.Drawing.Size(400, 200) };
        grid.Columns.Add("Name", 200);
        grid.Rows.Add("original");
        using var surface = new SkiaControlSurface(grid);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        var cell = ((IPlatformAccessibilityGrid)root).GetGridItem(0, 0)!;
        Assert.Contains(AndroidAccessibilityMapper.ActionSetText, AndroidAccessibilityMapper.Actions(cell));
        Assert.True(AndroidAccessibilityMapper.PerformAction(cell, AndroidAccessibilityMapper.ActionSetText, "edited"));
        Assert.Equal("edited", grid.Rows[0].Cells[0].Value);
        grid.ReadOnly = true;
        Assert.DoesNotContain(AndroidAccessibilityMapper.ActionSetText, AndroidAccessibilityMapper.Actions(cell));
        Assert.False(AndroidAccessibilityMapper.Read(cell).Editable);
    }

    [Fact]
    public void WindowlessDatePickerExposesTextCheckboxAndStepsWithoutPopup()
    {
        using var picker = new DateTimePicker { ShowCheckBox = true, Checked = false, ShowUpDown = true };
        using var surface = new SkiaControlSurface(picker);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        Assert.DoesNotContain(AndroidAccessibilityMapper.ActionExpand, AndroidAccessibilityMapper.Actions(root));
        Assert.Contains(AndroidAccessibilityMapper.ActionSetText, AndroidAccessibilityMapper.Actions(root));
        var date = new DateTime(2026, 9, 12);
        Assert.True(AndroidAccessibilityMapper.PerformAction(root, AndroidAccessibilityMapper.ActionSetText,
            date.ToString("d", System.Globalization.CultureInfo.CurrentCulture)));
        Assert.True(picker.Checked);
        Assert.Equal(date, picker.Value);
        Assert.True(AndroidAccessibilityMapper.PerformAction(root.GetChild(1)!, AndroidAccessibilityMapper.ActionClick, null));
        Assert.Equal(date.AddDays(1), picker.Value);
    }

    [Fact]
    public void CurrentGridMapsCollectionCellsHeadersAndSelectionWithoutExtraTree()
    {
        using var grid = new DataGridView { Size = new System.Drawing.Size(400, 200), RowHeadersVisible = true, SelectionMode = DataGridViewSelectionMode.CellSelect };
        grid.Columns.Add("Name", 140); grid.Columns.Add("Value", 140);
        grid.Rows.Add("a", "first"); grid.Rows.Add("z", "last");
        using var surface = new SkiaControlSurface(grid);
        var root = ((IPlatformAccessibilityHost)surface).AccessibilityRoot!;
        var collection = AndroidAccessibilityMapper.Collection(root)!.Value;
        Assert.Equal(2, collection.Rows); Assert.Equal(2, collection.Columns);
        Assert.Equal(1, collection.SelectionMode);
        Assert.Equal("android.widget.GridView", AndroidAccessibilityMapper.Read(root).ClassName);
        var cell = ((IPlatformAccessibilityGrid)root).GetGridItem(1, 1)!;
        var coordinates = AndroidAccessibilityMapper.GridCell(cell)!.Value;
        Assert.Equal(1, coordinates.Row); Assert.Equal(1, coordinates.Column);
        Assert.True(AndroidAccessibilityMapper.PerformAction(cell, AndroidAccessibilityMapper.ActionSelect, null));
        Assert.Equal(new System.Drawing.Point(1, 1), grid.CurrentCellAddress);
        grid.SortByColumn(0, SortOrder.Descending);
        Assert.Equal(0, AndroidAccessibilityMapper.GridCell(cell)!.Value.Row);
        grid.Rows.RemoveAt(0);
        Assert.Null(AndroidAccessibilityMapper.GridCell(cell));
        Assert.False(AndroidAccessibilityMapper.PerformAction(cell, AndroidAccessibilityMapper.ActionSelect, null));
    }

    [Theory]
    [InlineData(AccessibleControlType.Hyperlink, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Spinner, "android.widget.NumberPicker")]
    [InlineData(AccessibleControlType.DataGrid, "android.widget.GridView")]
    [InlineData(AccessibleControlType.DataItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Header, "android.view.ViewGroup")]
    [InlineData(AccessibleControlType.HeaderItem, "android.widget.TextView")]
    [InlineData(AccessibleControlType.Calendar, "android.widget.CalendarView")]
    public void AdditionalTypesKeepNativeClassMappingExplicit(AccessibleControlType type, string expected)
        => Assert.Equal(expected, AndroidAccessibilityMapper.ClassName((int)type));
}
