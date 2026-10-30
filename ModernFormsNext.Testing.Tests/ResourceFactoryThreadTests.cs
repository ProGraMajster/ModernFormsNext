using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class ResourceFactoryThreadTests
{
    [Fact]
    public void FactoryRejectsNonUiThreadBeforeInvokingUserCode()
    {
        using var host = ModernFormsTestHost.Create();
        var resources = new ResourceDictionary();
        int calls = 0;
        Exception? failure = null;
        var worker = new Thread(() => failure = Record.Exception(() => resources.SetFactory("key", () => ++calls)));
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(10)));
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(0, calls);
        Assert.Empty(resources);
        resources.SetFactory("key", () => ++calls);
        Assert.Equal(1, resources["key"]);
    }
}
