using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ModernFormsNext;
using ModernFormsNext.Virtualization;

namespace ControlGallery.Panels;

/// <summary>Demonstrates shared page recycling and an application-owned cancellable refresh command.</summary>
public sealed class CollectionUtilitiesPanel : BasePanel
{
    private readonly ObservableCollection<string> source = new(Enumerable.Range(0, 100_003).Select(index => $"Page {index:N0}"));
    private readonly CarouselView carousel;
    private readonly RefreshView refresh;
    private readonly Label status;
    private readonly AsyncCommand refreshCommand;
    private TaskCompletionSource? completion;
    private bool unloading;
    private int completed;

    /// <summary>Creates a bounded interactive carousel inside a refresh host, with explicit completion for inspection.</summary>
    public CollectionUtilitiesPanel()
    {
        var tools = Controls.Add(new Panel { Dock = DockStyle.Top, Height = 116 });
        var previous = tools.Controls.Add(new Button { Bounds = new(8, 8, 110, 34), Text = "Previous page" });
        var next = tools.Controls.Add(new Button { Bounds = new(126, 8, 110, 34), Text = "Next page" });
        var jump = tools.Controls.Add(new Button { Bounds = new(244, 8, 130, 34), Text = "Jump to 90000" });
        var axis = tools.Controls.Add(new Button { Bounds = new(382, 8, 130, 34), Text = "Horizontal / vertical" });
        var complete = tools.Controls.Add(new Button { Bounds = new(8, 50, 160, 34), Text = "Complete refresh" });
        var move = tools.Controls.Add(new Button { Bounds = new(176, 50, 160, 34), Text = "Move selected first" });
        tools.Controls.Add(new Label { Bounds = new(8, 88, 900, 24),
            Text = "Wheel / swipe pages; Ctrl+PageUp/Down. Pull downward at the top, click Refresh, or press F5." });
        status = Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 72, Multiline = true });
        refresh = Controls.Add(new RefreshView { Dock = DockStyle.Fill, AccessibleName = "Gallery refresh" });
        carousel = refresh.Controls.Add(new CarouselView { Dock = DockStyle.Fill, AccessibleName = "Gallery pages" });
        carousel.ItemTemplate = new PageTemplate(this);
        carousel.ItemsSource = source;
        refreshCommand = new AsyncCommand(async (_, token) =>
        {
            var pending = new TaskCompletionSource();
            completion = pending;
            // The application owns this finite demonstration operation. The control does
            // not create a timer/task or own completion; Cancel uses AsyncCommand's token.
            using var registration = token.Register(() => pending.TrySetCanceled(token));
            await pending.Task;
            if (!unloading) { completed++; ShowStatus(); }
        });
        refresh.Command = refreshCommand;
        refresh.RefreshStateChanged += (_, _) => ShowStatus();
        carousel.SelectedIndexChanged += (_, _) => ShowStatus();
        previous.Click += (_, _) => carousel.MovePrevious();
        next.Click += (_, _) => carousel.MoveNext();
        jump.Click += (_, _) => carousel.SelectedIndex = 90_000;
        axis.Click += (_, _) => carousel.PageOrientation = carousel.PageOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
        complete.Click += (_, _) => completion?.TrySetResult();
        move.Click += (_, _) => { if (carousel.SelectedIndex >= 0) source.Move(carousel.SelectedIndex, 0); ShowStatus(); };
        tools.BringToFront();
        status.BringToFront();
        ShowStatus();
    }

    private void ShowStatus()
    {
        if (unloading) return;
        var snapshot = carousel.GetVirtualizationSnapshot();
        status.Text = $"{snapshot.ItemCount:N0} pages / {snapshot.RealizedContainerCount} live / {snapshot.RecycledContainerCount} pooled; selected {carousel.SelectedIndex:N0}.\n" +
            $"Refreshing: {refresh.IsRefreshing}; cancel available: {refresh.CanCancelRefresh}; completed: {completed}. Use Complete refresh to finish the borrowed command.";
    }

    /// <inheritdoc/>
    public override void UnloadPanel()
    {
        unloading = true;
        // This sample is the command owner; disposing RefreshView alone never cancels it.
        if (refreshCommand.CanCancel) refreshCommand.Cancel();
        base.UnloadPanel();
    }

    private sealed class PageTemplate(CollectionUtilitiesPanel owner) : IItemContainerFactory<Control>
    {
        private readonly Dictionary<Control, int> indices = new();
        public Control CreateContainer()
        {
            var page = new Panel { Padding = new Padding(12), Margin = Padding.Empty };
            var open = page.Controls.Add(new Button { Dock = DockStyle.Bottom, Height = 42, Text = "Open this page" });
            page.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Multiline = true });
            open.BringToFront();
            open.Click += (_, _) => { if (indices.TryGetValue(page, out int index)) owner.status.Text = $"Opened {owner.source[index]}; the page button keeps normal keyboard and accessibility input."; };
            return page;
        }
        public void PrepareContainer(Control container, int index)
        {
            indices[container] = index;
            container.Controls.OfType<Label>().Single().Text = owner.source[index] + "\nRecycled interactive page";
        }
        public void ClearContainer(Control container, int previousIndex)
        {
            indices.Remove(container);
            container.Controls.OfType<Label>().Single().Text = string.Empty;
        }
        public void DiscardContainer(Control container) { indices.Remove(container); container.Dispose(); }
    }
}
