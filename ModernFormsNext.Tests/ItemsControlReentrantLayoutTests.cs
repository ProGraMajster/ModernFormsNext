using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ItemsControlReentrantLayoutTests
{
    [Fact]
    public void ExistingCollectionMetricChangeInsidePreparationDoesNotArrangeAnUnattachedRow()
    {
        using var view = new CollectionView();
        using var surface = new SkiaControlSurface(view);
        surface.Resize(320, 160);
        bool first = true;
        view.ItemTemplate = new ReentrantMetricFactory(() =>
        {
            if (!first) return;
            first = false;
            view.ItemSize = new(200, 40);
        });
        view.ItemsSource = Enumerable.Range(0, 100).Select(index => $"Item {index}").ToArray();
        view.PerformLayout();
        Assert.Equal(new System.Drawing.Size(200, 40), view.ItemSize);
        Assert.InRange(view.GetVirtualizationSnapshot().RealizedContainerCount, 1, 16);
        Assert.NotNull(view.GetItemContainer(0));
    }

    private sealed class ReentrantMetricFactory(Action prepare) : Virtualization.IItemContainerFactory<Control>
    {
        public Control CreateContainer() => new Label();
        public void PrepareContainer(Control container, int index) { prepare(); container.Text = $"Item {index}"; }
        public void ClearContainer(Control container, int index) { }
        public void DiscardContainer(Control container) => container.Dispose();
    }
}
