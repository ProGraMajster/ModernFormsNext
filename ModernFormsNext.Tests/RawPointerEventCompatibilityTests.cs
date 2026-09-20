using ModernFormsNext.WindowKit.Input.Raw;
using Xunit;

namespace ModernFormsNext.Tests;

public class RawPointerEventCompatibilityTests
{
    // These values were published in 1.10.0. Existing compiled backends and persisted
    // input traces must retain their meaning when a later release adds an event kind.
    [Theory]
    [InlineData(nameof(RawPointerEventType.LeaveWindow), 0)]
    [InlineData(nameof(RawPointerEventType.LeftButtonDown), 1)]
    [InlineData(nameof(RawPointerEventType.LeftButtonUp), 2)]
    [InlineData(nameof(RawPointerEventType.RightButtonDown), 3)]
    [InlineData(nameof(RawPointerEventType.RightButtonUp), 4)]
    [InlineData(nameof(RawPointerEventType.MiddleButtonDown), 5)]
    [InlineData(nameof(RawPointerEventType.MiddleButtonUp), 6)]
    [InlineData(nameof(RawPointerEventType.XButton1Down), 7)]
    [InlineData(nameof(RawPointerEventType.XButton1Up), 8)]
    [InlineData(nameof(RawPointerEventType.XButton2Down), 9)]
    [InlineData(nameof(RawPointerEventType.XButton2Up), 10)]
    [InlineData(nameof(RawPointerEventType.Move), 11)]
    [InlineData(nameof(RawPointerEventType.Wheel), 12)]
    [InlineData(nameof(RawPointerEventType.NonClientLeftButtonDown), 13)]
    [InlineData(nameof(RawPointerEventType.TouchBegin), 14)]
    [InlineData(nameof(RawPointerEventType.TouchUpdate), 15)]
    [InlineData(nameof(RawPointerEventType.TouchEnd), 16)]
    [InlineData(nameof(RawPointerEventType.TouchCancel), 17)]
    [InlineData(nameof(RawPointerEventType.Magnify), 18)]
    [InlineData(nameof(RawPointerEventType.Rotate), 19)]
    [InlineData(nameof(RawPointerEventType.Swipe), 20)]
    public void PublishedEventValuesRemainCompatible(string name, int publishedValue)
    {
        Assert.Equal(publishedValue, (int)Enum.Parse<RawPointerEventType>(name));
    }

    [Fact]
    public void CaptureLostUsesANewValueWithoutAliasingPublishedEvents()
    {
        Assert.Equal(21, (int)RawPointerEventType.CaptureLost);
        Assert.Equal(Enum.GetNames<RawPointerEventType>().Length,
            Enum.GetValues<RawPointerEventType>().Distinct().Count());
    }
}
