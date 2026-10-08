using ModernFormsNext.WindowKit.Backend.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using Xunit;

// These deterministic host tests retain the creating UI thread through disposal. Fake service
// operations complete inline; the concurrent-update workers never wait for dispatcher work.
#pragma warning disable xUnit1031

namespace ModernFormsNext.Testing.Tests;

public sealed class SystemNotificationTests
{
    private static SystemNotification Download(string id = "download-42", string group = "downloads") => new()
    {
        Id = id, Title = "Downloading", Message = "Video", Group = group,
        Progress = new() { Value = .1, Status = "Downloading" },
        Actions = [new("Open", "open") { Id = "open-file" }, new("Folder", "folder")]
    };

    [Fact]
    public void ShowUpdateReplaceAndDismissUseOneIdentity()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        Assert.Same(service, SystemNotifications.Service);
        var first = SystemNotifications.ShowAsync(Download()).GetAwaiter().GetResult();
        Assert.True(first.IsAccepted);
        Assert.Equal("download-42", first.Key!.Id);
        for (uint i = 2; i <= 100; i++)
            Assert.True(service.UpdateAsync(first.Key, new() { Value = i / 100d, Status = "Downloading" }).Result.IsAccepted);
        Assert.Single(service.Notifications);
        Assert.Equal(1d, service.Notifications[first.Key].Progress!.Value);
        var done = Download() with { Title = "Complete", Progress = null };
        Assert.Equal(first.Key, service.ShowAsync(done).Result.Key);
        Assert.Single(service.Notifications);
        Assert.Null(service.Notifications[first.Key].Progress);
        Assert.True(service.DismissAsync(first.Key).Result.IsAccepted);
        Assert.Empty(service.GetHistoryAsync().Result);
        Assert.Equal(SystemNotificationStatus.NotFound, service.UpdateAsync(first.Key, new() { Value = 1, Status = "Done"}).Result.Status);
    }

    [Fact]
    public void SameLogicalIdInDifferentGroupsRemainsIndependent()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.ShowAsync(Download("same", "a")).GetAwaiter().GetResult();
        service.ShowAsync(Download("same", "b")).GetAwaiter().GetResult();
        service.ShowAsync(Download("other", "b")).GetAwaiter().GetResult();
        Assert.Equal(3, service.GetHistoryAsync().Result.Count);
        service.RemoveByIdAsync("same").GetAwaiter().GetResult();
        Assert.Single(service.Notifications);
        service.RemoveByGroupAsync("b").GetAwaiter().GetResult();
        Assert.Empty(service.Notifications);
        service.ShowAsync(Download()).GetAwaiter().GetResult();
        service.ClearAsync().GetAwaiter().GetResult();
        Assert.Empty(service.Notifications);
    }

    [Fact]
    public void NullRemovalSelectorsCannotClearHistory()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var key = service.ShowAsync(Download()).GetAwaiter().GetResult().Key!;
        Assert.Throws<ArgumentNullException>(() => { _ = service.DismissAsync(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = service.RemoveByIdAsync(null!); });
        Assert.Throws<ArgumentNullException>(() => { _ = service.RemoveByGroupAsync(null!); });
        Assert.Equal(key, Assert.Single(service.GetHistoryAsync().GetAwaiter().GetResult()).Key);
    }

    [Fact]
    public void ActivationCopiesInputsAndEntersExistingLifecycle()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var key = service.ShowAsync(Download()).Result.Key!;
        var input = new Dictionary<string, string> { ["reply"] = "Hello", ["choice"] = "one" };
        var events = new List<SystemNotificationActivation>();
        service.Activated += (_, activation) => events.Add(activation);
        service.Activate(key, "open-file", input);
        input["reply"] = "mutated";
        Assert.Empty(events);
        host.Dispatcher.Drain();
        Assert.Equal("Hello", Assert.Single(events).UserInput["reply"]);
        Assert.Equal("open", events[0].ActivationData);
        Assert.Equal(PlatformActivationKind.Notification, Application.Lifecycle.LastActivation!.Kind);
        Assert.Equal("download-42", Application.Lifecycle.LastActivation.Notification!.NotificationId);
        Assert.Equal("open-file", Application.Lifecycle.LastActivation.Notification.ActionId);
        service.Activate(key);
        host.Dispatcher.Drain();
        Assert.Null(events[1].ActionId);
    }

    [Fact]
    public void UnsupportedContentDegradesOrRejectsWithoutMutatingCaller()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.SetCapabilities(SystemNotificationFeatures.Basic);
        var request = Download() with { Images = [new("C:/image.png", SystemNotificationImageRole.Thumbnail)], Inputs = [new("reply")] };
        var result = service.ShowAsync(request).Result;
        Assert.True(result.IsAccepted);
        Assert.NotEmpty(result.Warnings!);
        Assert.Empty(service.Notifications[result.Key!].Actions);
        Assert.Null(service.Notifications[result.Key!].Progress);
        Assert.Equal(2, request.Actions.Count);
        Assert.NotNull(request.Progress);
        Assert.Equal(SystemNotificationStatus.Unsupported, service.ShowAsync(request with { Id = "strict", AllowDegradation = false }).Result.Status);
        Assert.Single(service.Notifications);
        service.SetCapabilities(SystemNotificationFeatures.None);
        Assert.False(SystemNotifications.IsSupported);
        Assert.Equal(SystemNotificationStatus.Unsupported, service.ShowAsync(request).Result.Status);
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidProgressNeverReachesTransport(double value)
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        Assert.Equal(SystemNotificationStatus.Invalid, service.ShowAsync(Download() with { Progress = new() { Value = value, Status = "Bad" } }).Result.Status);
        Assert.Empty(service.Notifications);
    }

    [Fact]
    public void ConcurrentUpdatesAreSerializedAndDoNotReshow()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var key = service.ShowAsync(Download()).Result.Key!;
        var work = Enumerable.Range(2, 200).Reverse().Select(i => Task.Run(() => service.UpdateAsync(key,
            new() { Value = i / 201d, Status = "Working" }))).ToArray();
        Task.WhenAll(work).GetAwaiter().GetResult();
        Assert.All(work, task => Assert.True(task.Result.IsAccepted));
        Assert.True(service.UpdateAsync(key, new() { Value = 1, Status = "Done" }).Result.IsAccepted);
        Assert.Equal("Done", service.Notifications[key].Progress!.Status);
        Assert.Single(service.Notifications);
    }

    [Fact]
    public void IndeterminateAndUserDismissalUseNativeContract()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var key = service.ShowAsync(Download() with { Progress = new() { Status = "Preparing" } }).Result.Key!;
        Assert.Null(service.Notifications[key].Progress!.Value);
        Assert.True(service.UpdateAsync(key, new() { Value = .5, Status = "Working"}).Result.IsAccepted);
        SystemNotificationChange? change = null;
        service.Changed += (_, e) => change = e;
        service.DismissByUser(key);
        host.Dispatcher.Drain();
        Assert.Equal("UserCanceled", change!.Reason);
        Assert.Equal(SystemNotificationStatus.NotFound, service.UpdateAsync(key, new() { Status = "Old"}).Result.Status);
    }

    [Fact]
    public void CancellationAndDisposalSuppressPendingCallbacks()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => service.ShowAsync(Download(), cancellation.Token).GetAwaiter().GetResult());
        Assert.Empty(service.Notifications);
        var key = service.ShowAsync(Download()).Result.Key!;
        int callbacks = 0;
        service.Activated += (_, _) => callbacks++;
        service.Activate(key);
        service.DisposeAsync().GetAwaiter().GetResult();
        host.Dispatcher.Drain();
        Assert.Equal(0, callbacks);
        Assert.Throws<ObjectDisposedException>(() => { _ = service.ShowAsync(Download()); });
        service.DisposeAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void CallerListAndReturnedSnapshotsCannotMutateStoredContent()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var actions = new List<SystemNotificationAction> { new("Original", "original") };
        var result = service.ShowAsync(Download() with { Actions = actions }).Result;
        actions.Clear();
        Assert.Single(service.Notifications[result.Key!].Actions);
        ((SystemNotificationAction[])service.Notifications[result.Key!].Actions)[0] = new("Mutated", "mutated");
        Assert.Equal("Original", service.Notifications[result.Key!].Actions[0].Title);
    }

    [Fact]
    public void InvalidInputsAndIdentifiersAreRejected()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        Assert.Equal(SystemNotificationStatus.Invalid, service.ShowAsync(Download() with { Id = new string('x', 4097) }).Result.Status);
        Assert.Equal(SystemNotificationStatus.Invalid, service.ShowAsync(Download() with { Inputs = [new("same"), new("same")] }).Result.Status);
        Assert.Equal(SystemNotificationStatus.Invalid, service.ShowAsync(Download() with { Inputs = [new("choice") { Choices = [new("a", "A")], DefaultValue = "b" }] }).Result.Status);
        Assert.Equal(SystemNotificationStatus.Invalid, service.ShowAsync(Download() with { Message = "\0" }).Result.Status);
        Assert.Empty(service.Notifications);
    }
}
