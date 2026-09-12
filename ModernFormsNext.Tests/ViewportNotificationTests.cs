using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ViewportNotificationTests
{
    [Fact]
    public void RangeChangeReportsOwnerAndCanonicalObserverFailuresAfterBothRan()
    {
        using var bar = new HorizontalScrollBar();
        bar.RangeMetadataChanged += (_, _) => throw new InvalidOperationException("owner");
        bar.AccessibilityObject.ClientNotification += (_, e) =>
        {
            if (e.EventId == AccessibleEvents.RangeValueChanged) throw new InvalidOperationException("canonical");
        };
        var failure = Assert.Throws<AggregateException>(() => bar.SmallChange = 3);
        Assert.Equal(3, bar.SmallChange);
        Assert.Equal(new[] { "owner", "canonical" }, failure.Flatten().InnerExceptions.Select(error => error.Message));
    }
}
