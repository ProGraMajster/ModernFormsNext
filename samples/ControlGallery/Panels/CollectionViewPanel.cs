using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ModernFormsNext;
using ModernFormsNext.Virtualization;

namespace ControlGallery.Panels;

/// <summary>Demonstrates the public CollectionView source/template API with a large mutable collection.</summary>
public sealed class CollectionViewPanel : BasePanel
{
    private readonly ObservableCollection<Entry> entries;
    private readonly CollectionView view;
    private readonly Label status;

    /// <summary>Creates 100,003 borrowed entries and a bounded, bound, interactive row template.</summary>
    public CollectionViewPanel()
    {
        entries = new(Enumerable.Range(0, 100_003).Select(index => new Entry($"Entry {index:N0}")));
        var body = Controls.Add(new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) });
        view = body.Controls.Add(new CollectionView
        {
            Dock = DockStyle.Fill, ItemSize = new(380, 48), CacheItemCount = 2,
            BindingContext = new ModernFormsNext.DataBinding.BindingContext(), AccessibleName = "Collection entries"
        });
        status = new Label { Dock = DockStyle.Bottom, Height = 64, Multiline = true };
        view.ItemTemplate = new RowTemplate(view, (entry, index) =>
        {
            view.SelectedIndex = index;
            status.Text = $"Opened {entry.Title}. The template button kept its canonical focus/command path.";
        });
        view.ItemsSource = entries;
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 92 });
        var jump = tools.Controls.Add(new Button { Bounds = new(8, 8, 140, 34), Text = "Jump to 90000" });
        var move = tools.Controls.Add(new Button { Bounds = new(156, 8, 140, 34), Text = "Move selected" });
        var rename = tools.Controls.Add(new Button { Bounds = new(304, 8, 140, 34), Text = "Rename selected" });
        var wrap = tools.Controls.Add(new Button { Bounds = new(452, 8, 140, 34), Text = "Stack / wrap" });
        tools.Controls.Add(new Label { Bounds = new(8, 50, 900, 32),
            Text = "Single occurrence selection; Home/End/arrows, Enter/Space, wheel/touch. Row buttons remain interactive." });
        Controls.Add(status);
        status.BringToFront();
        tools.BringToFront();
        jump.Click += (_, _) => { view.SelectedIndex = 90_000; view.ScrollToItem(90_000); ShowDiagnostics(); };
        move.Click += (_, _) =>
        {
            if (view.SelectedIndex >= 0)
            {
                entries.Move(view.SelectedIndex, 0);
                // A source move preserves the viewport anchor. This explicit UI action also
                // reveals the moved selection so its bound template remains inspectable.
                view.ScrollToItem(view.SelectedIndex);
            }
            ShowDiagnostics();
        };
        rename.Click += (_, _) => { if (view.SelectedItem is Entry entry) entry.Title += " updated"; ShowDiagnostics(); };
        wrap.Click += (_, _) => { view.ItemsLayout = view.ItemsLayout == ItemsLayout.Stack ? ItemsLayout.Wrap : ItemsLayout.Stack; ShowDiagnostics(); };
        view.SelectedIndexChanged += (_, _) => ShowDiagnostics();
        view.ItemInvoked += (_, e) => status.Text = $"Activated occurrence {e.Index:N0}: {((Entry?)e.Item)?.Title}";
        ShowDiagnostics();
    }

    private void ShowDiagnostics()
    {
        var snapshot = view.GetVirtualizationSnapshot();
        status.Text = $"CollectionView: {snapshot.ItemCount:N0} logical / {snapshot.RealizedContainerCount} live / {snapshot.RecycledContainerCount} pool; " +
            $"created {snapshot.CreatedContainerCount}, reused {snapshot.ReusedContainerCount}; selected index {view.SelectedIndex:N0}.\n" +
            "The source and factory are borrowed. Descendant bindings are retired before a row is rebound.";
    }

    private sealed class Entry(string title) : INotifyPropertyChanged
    {
        public string Title { get => title; set { title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class RowTemplate(CollectionView owner, Action<Entry, int> open) : IItemContainerFactory<Control>
    {
        private readonly Dictionary<Control, (Entry Item, int Index)> bindings = new();
        public Control CreateContainer()
        {
            var row = new Row(owner, bindings) { Margin = Padding.Empty, Padding = new Padding(4) };
            var button = row.Controls.Add(new Button { Dock = DockStyle.Right, Width = 72, Text = "Open" });
            row.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
            button.BringToFront();
            button.Click += (_, _) => { if (bindings.TryGetValue(row, out var binding)) open(binding.Item, binding.Index); };
            return row;
        }
        public void PrepareContainer(Control container, int index)
        {
            var entry = (Entry)owner.ItemsSource![index]!;
            bindings[container] = (entry, index);
            container.DataBindings.Add(nameof(Control.AccessibleName), entry, nameof(Entry.Title));
            container.Controls.OfType<Label>().Single().DataBindings.Add(nameof(Control.Text), entry, nameof(Entry.Title));
        }
        public void ClearContainer(Control container, int previousIndex)
        {
            bindings.Remove(container);
            container.AccessibleName = null;
            foreach (var label in container.Controls.OfType<Label>()) label.Text = string.Empty;
        }
        public void DiscardContainer(Control container) { bindings.Remove(container); container.Dispose(); }
    }

    private sealed class Row(CollectionView owner, Dictionary<Control, (Entry Item, int Index)> bindings) : Panel
    {
        /// <inheritdoc/>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (bindings.TryGetValue(this, out var binding) && owner.SelectedIndex == binding.Index)
                e.Canvas.FillRectangle(ClientRectangle, Theme.ControlHighlightLowColor);
        }
        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Focused && ShowFocusCues) e.Canvas.DrawFocusRectangle(ClientRectangle, 1);
        }
    }
}
