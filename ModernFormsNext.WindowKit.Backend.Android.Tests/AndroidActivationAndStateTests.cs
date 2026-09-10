using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Lifecycle;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidActivationAndStateTests
{
    [Theory]
    [InlineData(null, PlatformActivationKind.Launch)]
    [InlineData("https://example.com/document", PlatformActivationKind.Uri)]
    [InlineData("modernformsnext-sample://open/item", PlatformActivationKind.Protocol)]
    [InlineData("content://provider/document/42", PlatformActivationKind.Files)]
    [InlineData("file:///storage/document.txt", PlatformActivationKind.Files)]
    public void IntentDataMapsWithoutOpeningResourcesOrGuessingContentProviderPaths(string? data, PlatformActivationKind kind)
    {
        PlatformApplicationActivation activation = AndroidActivationMapper.Create(data);
        Assert.Equal(kind, activation.Kind);
        if (kind == PlatformActivationKind.Files) Assert.Equal(data, Assert.Single(activation.Files));
        else if (data is not null) Assert.Equal(data, activation.Uri!.OriginalString);
    }

    [Fact]
    public void SharedStreamUrisAreDetachedDeduplicatedAndBounded()
    {
        string[] streams = ["content://provider/first", "content://provider/first", "content://provider/second"];
        var activation = AndroidActivationMapper.Create(null, streams);
        streams[0] = "changed";
        Assert.Equal(new[] { "content://provider/first", "content://provider/second" }, activation.Files);
        Assert.Throws<ArgumentException>(() => AndroidActivationMapper.Create(null, ["relative"]));
        Assert.Throws<ArgumentException>(() => AndroidActivationMapper.Create("relative"));
        Assert.Throws<ArgumentException>(() => AndroidActivationMapper.Create(null,
            Enumerable.Range(0, PlatformApplicationActivation.MaximumItemCount + 1).Select(i => $"content://provider/{i}")));
        Assert.Throws<ArgumentException>(() => AndroidActivationMapper.Create(
            "content://provider/" + new string('a', PlatformApplicationActivation.MaximumItemLength)));
        Assert.Throws<ArgumentException>(() => AndroidActivationMapper.Create(null,
            Enumerable.Repeat("content://provider/same", PlatformApplicationActivation.MaximumItemCount * 2 + 2)));
    }

    [Fact]
    public void StateRoundTripPreservesOnlyVersionedApplicationStrings()
    {
        var state = new PlatformApplicationStateData(3, new Dictionary<string, string>
        {
            ["clickCount"] = "12", ["unicode"] = "Łódź 😀\n", ["empty"] = string.Empty
        });
        var restored = AndroidLifecycleStateCodec.Deserialize(state.Version, AndroidLifecycleStateCodec.Serialize(state));
        Assert.Equal(3, restored.Version);
        Assert.Equal(state.Values, restored.Values);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"value\":42}")]
    [InlineData("{\"value\":null}")]
    [InlineData("{\"value\":\"a\",\"value\":\"b\"}")]
    public void RestoreRejectsInvalidOrDuplicatePayloads(string serialized)
        => Assert.Throws<ArgumentException>(() => AndroidLifecycleStateCodec.Deserialize(1, serialized));
}
