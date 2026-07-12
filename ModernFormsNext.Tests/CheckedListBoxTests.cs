using System.Drawing;
using ModernFormsNext;
using Xunit;

namespace ModernFormsNext.Tests;

public class CheckedListBoxTests
{
    [Fact]
    public void ItemsAreUncheckedByDefault()
    {
        var list = CreateList("Read", "Write");

        Assert.False(list.GetItemChecked(0));
        Assert.False(list.GetItemChecked(1));
        Assert.Equal(CheckState.Unchecked, list.GetItemCheckState(0));
        Assert.Empty(list.CheckedItems);
        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void SetItemCheckedUpdatesCheckedViews()
    {
        var list = CreateList("Read", "Write", "Admin");

        list.SetItemChecked(0, true);
        list.SetItemChecked(2, true);

        Assert.True(list.GetItemChecked(0));
        Assert.Equal(new[] { 0, 2 }, list.CheckedIndices.ToArray());
        Assert.Equal(new object[] { "Read", "Admin" }, list.CheckedItems.ToArray());

        list.SetItemChecked(0, false);

        Assert.False(list.GetItemChecked(0));
        Assert.Equal(new[] { 2 }, list.CheckedIndices.ToArray());
    }

    [Fact]
    public void SetItemCheckStateSupportsIndeterminate()
    {
        var list = CreateList("Read", "Write");

        list.SetItemCheckState(1, CheckState.Indeterminate);

        Assert.True(list.GetItemChecked(1));
        Assert.Equal(CheckState.Indeterminate, list.GetItemCheckState(1));
        Assert.Equal(new[] { 1 }, list.CheckedIndices.ToArray());
        Assert.Equal(new object[] { "Write" }, list.CheckedItems.ToArray());
    }

    [Fact]
    public void ItemCheckFiresBeforeValueIsCommittedAndCanChangeNewValue()
    {
        var list = CreateList("Read");
        var event_count = 0;

        list.ItemCheck += (_, e) =>
        {
            event_count++;
            Assert.Equal(0, e.Index);
            Assert.Equal(CheckState.Unchecked, e.CurrentValue);
            Assert.Equal(CheckState.Checked, e.NewValue);
            Assert.Equal(CheckState.Unchecked, list.GetItemCheckState(0));

            e.NewValue = CheckState.Indeterminate;
        };

        list.SetItemChecked(0, true);

        Assert.Equal(1, event_count);
        Assert.Equal(CheckState.Indeterminate, list.GetItemCheckState(0));
    }

    [Fact]
    public void CheckOnClickControlsWhetherItemTextClicksToggleState()
    {
        var list = CreateList("Read", "Write");

        list.ClickItemText(0);

        Assert.Equal(0, list.SelectedIndex);
        Assert.False(list.GetItemChecked(0));

        list.ClickItemText(0);

        Assert.True(list.GetItemChecked(0));

        list.SetItemChecked(0, false);
        list.ClickCheckBox(0);

        Assert.True(list.GetItemChecked(0));

        list.SetItemChecked(0, false);
        list.CheckOnClick = true;
        list.ClickItemText(1);

        Assert.True(list.GetItemChecked(1));
    }

    [Fact]
    public void RemoveAtRemovesTheStateForTheRemovedItem()
    {
        var list = CreateList("Read", "Write", "Admin");

        list.SetItemChecked(1, true);
        list.Items.RemoveAt(1);

        Assert.Equal(new object[] { "Read", "Admin" }, list.Items.ToArray());
        Assert.Empty(list.CheckedItems);
        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void RemoveRemovesTheStateForTheRemovedItem()
    {
        var list = CreateList("Read", "Write", "Admin");

        list.SetItemChecked(1, true);
        list.Items.Remove("Write");

        Assert.Equal(new object[] { "Read", "Admin" }, list.Items.ToArray());
        Assert.Empty(list.CheckedItems);
        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void RemovingEarlierItemKeepsCheckedStateWithShiftedItem()
    {
        var list = CreateList("Read", "Write", "Admin");

        list.SetItemChecked(1, true);
        list.Items.RemoveAt(0);

        Assert.Equal("Write", list.Items[0]);
        Assert.True(list.GetItemChecked(0));
        Assert.Equal(new object[] { "Write" }, list.CheckedItems.ToArray());
    }

    [Fact]
    public void InsertKeepsCheckedStateWithOriginalItem()
    {
        var list = CreateList("Read", "Write");

        list.SetItemChecked(1, true);
        list.Items.Insert(0, "Preview");

        Assert.Equal(CheckState.Unchecked, list.GetItemCheckState(0));
        Assert.Equal("Write", list.Items[2]);
        Assert.True(list.GetItemChecked(2));
        Assert.Equal(new[] { 2 }, list.CheckedIndices.ToArray());
    }

    [Fact]
    public void ClearRemovesAllCheckedState()
    {
        var list = CreateList("Read", "Write");

        list.SetItemChecked(0, true);
        list.SetItemCheckState(1, CheckState.Indeterminate);
        list.Items.Clear();

        Assert.Empty(list.CheckedItems);
        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void CheckedStateDoesNotChangeSelectionState()
    {
        var list = CreateList("Read", "Write");

        Assert.Equal(-1, list.SelectedIndex);

        list.SetItemChecked(0, true);

        Assert.Equal(-1, list.SelectedIndex);

        list.SelectedIndex = 1;

        Assert.True(list.GetItemChecked(0));
        Assert.False(list.GetItemChecked(1));
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void SpaceTogglesFocusedItemWithoutChangingSelection()
    {
        var list = CreateList("Read", "Write");

        list.SelectedIndex = 1;
        list.PressKey(Keys.Space);

        Assert.True(list.GetItemChecked(1));
        Assert.Equal(1, list.SelectedIndex);
    }

    private static TestCheckedListBox CreateList(params object[] items)
    {
        var list = new TestCheckedListBox
        {
            Width = 200,
            Height = 100,
            ItemHeight = 20
        };

        list.Items.AddRange(items);
        return list;
    }

    private sealed class TestCheckedListBox : CheckedListBox
    {
        public void ClickCheckBox(int index)
        {
            var bounds = GetItemRectangle(index);
            ClickAt(new Point(bounds.Left + LogicalToDeviceUnits(8), bounds.Top + bounds.Height / 2));
        }

        public void ClickItemText(int index)
        {
            var bounds = GetItemRectangle(index);
            ClickAt(new Point(bounds.Left + LogicalToDeviceUnits(80), bounds.Top + bounds.Height / 2));
        }

        public void PressKey(Keys key)
        {
            OnKeyUp(new KeyEventArgs(key));
        }

        private void ClickAt(Point location)
        {
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, location.X, location.Y, Point.Empty));
        }
    }
}
