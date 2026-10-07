using ModernFormsNext.WindowKit.Backend.Android.Services;
using ModernFormsNext.WindowKit.Platform.Services;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class AndroidServiceContractTests
{
    [Theory]
    [InlineData("*.txt", "text/plain")]
    [InlineData("*.PNG", "image/png")]
    [InlineData("*.pdf", "application/pdf")]
    [InlineData("*.*", "*/*")]
    [InlineData("*.unknown", "*/*")]
    [InlineData("image??.png", "*/*")]
    public void GlobsAreMappedOrExplicitlyWidened(string pattern, string expected)
        => Assert.Equal([expected], AndroidServicePlans.MimeTypes([new("files") { Patterns = [pattern] }]));

    [Fact]
    public void ExplicitMimeWinsAndMultipleTypesRemainDistinctInOrder()
    {
        var filters = new[] { new FilePickerFileType("images") { MimeTypes = ["image/png", "image/jpeg", "image/png"], Patterns = ["*.bad"] } };
        Assert.Equal(["image/png", "image/jpeg"], AndroidServicePlans.MimeTypes(filters));
    }

    [Fact]
    public void ExtensionLookupUsesNativeFallbackWithoutTreatingGlobAsMime()
        => Assert.Equal(["application/x-custom"], AndroidServicePlans.MimeTypes(
            [new("custom") { Patterns = ["*.custom"] }], ext => ext == "custom" ? "application/x-custom" : null));

    [Theory]
    [InlineData("*.png")][InlineData("png")][InlineData("image/")][InlineData("*/png")][InlineData("text/plain;token=secret")]
    public void InvalidMimeRejected(string mime) => Assert.Throws<ArgumentException>(() => AndroidServicePlans.ValidateMime(mime));

    [Fact]
    public void ClipDataPreservesOrderAndRemovesExactDuplicates()
    {
        var first = new Uri("content://test/document/one");
        var second = new Uri("content://test/document/two");
        Assert.Equal([first, second], AndroidServicePlans.UniqueContentUris([first, second, first,
            new Uri("file:///private/item"), new Uri("relative", UriKind.Relative)]));
    }

    [Theory]
    [InlineData("http://example.com", true)][InlineData("https://example.com", true)]
    [InlineData("mailto:test@example.com", true)][InlineData("tel:123", true)]
    [InlineData("custom:hello", true)][InlineData("file:///private/test", false)]
    [InlineData("content://test/document", false)][InlineData("javascript:alert(1)", false)]
    [InlineData("intent://test", false)]
    public void ExternalUriPolicyIsDeterministic(string uri, bool expected)
        => Assert.Equal(expected, AndroidServicePlans.IsExternalUri(new Uri(uri)));

    [Theory]
    [InlineData(null, null)][InlineData(-1L, 0L)][InlineData(null, long.MaxValue)]
    public void MissingOrInvalidMetadataStaysUnknown(long? size, long? modified)
    {
        var properties = AndroidStorageMetadata.Create(size, modified);
        Assert.Null(properties.Size);
        Assert.Null(properties.DateCreated);
        Assert.Null(properties.DateModified);
    }

    [Fact]
    public void ProviderMetadataUsesBytesAndUnixMilliseconds()
    {
        var properties = AndroidStorageMetadata.Create(23, 1000);
        Assert.Equal(23UL, properties.Size);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1), properties.DateModified);
        Assert.Null(properties.DateCreated);
    }

    [Fact]
    public void SingleDataFallbackAndMissingDataAreDeterministic()
    {
        var data = new Uri("content://test/document/fallback");
        Assert.Equal([data], AndroidServicePlans.SelectUris([new Uri("file:///private/item")], data));
        Assert.Empty(AndroidServicePlans.SelectUris([], null));
    }

    [Fact]
    public void PendingRequestDoesNotRetainHost()
    {
        var state = new NativeRequestCoordinator<object, int>();
        var weak = BeginWeak(state);
        for (int i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        Assert.False(weak.IsAlive);
        state.Shutdown();
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference BeginWeak(NativeRequestCoordinator<object, int> state)
    {
        var host = new object();
        var request = state.Begin(host, default);
        // Observe cleanup's fault without keeping the host alive in a test closure.
        _ = request.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return new WeakReference(host);
    }

    [Fact]
    public void SharePlansUseReadGrantsOnlyForContentAttachments()
    {
        var text = AndroidServicePlans.Share(new() { Text = "synthetic" })!;
        Assert.Equal("android.intent.action.SEND", text.Action);
        Assert.Equal("text/plain", text.Mime);
        Assert.False(text.GrantReadAccess);
        Assert.Empty(text.Items);
        var first = new Uri("content://test/document/one");
        var second = new Uri("content://test/document/two");
        var single = AndroidServicePlans.Share(new() { Items = [first, first], MimeType = "IMAGE/PNG" })!;
        Assert.Equal("android.intent.action.SEND", single.Action);
        Assert.Equal("image/png", single.Mime);
        Assert.True(single.GrantReadAccess);
        Assert.Single(single.Items);
        var multiple = AndroidServicePlans.Share(new() { Items = [first, second] })!;
        Assert.Equal("android.intent.action.SEND_MULTIPLE", multiple.Action);
        Assert.Equal("*/*", multiple.Mime);
        Assert.Equal([first, second], multiple.Items);
        Assert.True(multiple.GrantReadAccess);
    }

    [Fact]
    public void ShareRejectsRawPathsAndBoundsAttachmentsBeforeLaunching()
    {
        Assert.Null(AndroidServicePlans.Share(new() { Items = [new Uri("file:///private/item")] }));
        Assert.Null(AndroidServicePlans.Share(new() { Items = [new Uri("https://example.com")] }));
        Assert.Throws<ArgumentException>(() => AndroidServicePlans.Share(new()));
        Assert.Throws<ArgumentException>(() => AndroidServicePlans.Share(new()
        { Items = Enumerable.Repeat(new Uri("content://test/item"), 33).ToArray() }));
    }

    [Theory]
    [InlineData("tel:123", "android.intent.action.DIAL")]
    [InlineData("https://example.com", "android.intent.action.VIEW")]
    [InlineData("mailto:test@example.com", "android.intent.action.VIEW")]
    public void LauncherPlansNeverUseCall(string uri, string action)
        => Assert.Equal(action, AndroidServicePlans.ViewAction(new Uri(uri)));

    [Fact]
    public void RelativeUriIsInvalid() => Assert.Throws<ArgumentException>(() => AndroidServicePlans.IsExternalUri(new Uri("relative", UriKind.Relative)));

    [Theory]
    [InlineData("document", "txt", "document.txt")][InlineData("document.txt", "pdf", "document.txt")]
    [InlineData(null, ".txt", "document.txt")]
    public void SaveExtensionDoesNotDuplicate(string? name, string ext, string expected)
        => Assert.Equal(expected, AndroidServicePlans.SuggestedName(name, ext));

    [Fact]
    public async Task OneRequestOnlyAndStaleOrWrongHostCannotComplete()
    {
        var state = new NativeRequestCoordinator<object, int>();
        var old = new object(); var current = new object();
        var first = state.Begin(old, default);
        Assert.Equal(PlatformServiceStatus.Busy, Assert.Throws<PlatformServiceException>(() => state.Begin(current, default)).Status);
        Assert.False(state.Complete(current, first.Code, 9));
        state.Destroy(old);
        Assert.Equal(PlatformServiceStatus.HostLost, (await Assert.ThrowsAsync<PlatformServiceException>(() => first.Task)).Status);
        var second = state.Begin(current, default);
        Assert.NotEqual(first.Code, second.Code);
        Assert.False(state.Complete(old, first.Code, 8));
        Assert.False(state.Complete(current, first.Code, 8));
        Assert.True(state.Complete(current, second.Code, 7));
        Assert.Equal(7, await second.Task);
        Assert.False(state.Busy);
    }

    [Fact]
    public async Task CancellationCompletesTaskButCannotOpenAnotherNativeModal()
    {
        var state = new NativeRequestCoordinator<object, int>(); var host = new object();
        using var cancel = new CancellationTokenSource();
        var first = state.Begin(host, cancel.Token); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.Task);
        Assert.True(state.Busy);
        Assert.Equal(PlatformServiceStatus.Busy, Assert.Throws<PlatformServiceException>(() => state.Begin(host, default)).Status);
        Assert.True(state.Complete(host, first.Code, 3));
        Assert.False(state.Busy);
    }

    [Fact]
    public async Task FailedLaunchReleasesSlotAndShutdownFinishesWaitingTask()
    {
        var state = new NativeRequestCoordinator<object, int>(); var host = new object();
        var first = state.Begin(host, default);
        state.FailStart(first.Code, new IOException("native failure"));
        await Assert.ThrowsAsync<IOException>(() => first.Task);
        var second = state.Begin(host, default);
        state.Shutdown(); state.Shutdown();
        Assert.Equal(PlatformServiceStatus.Shutdown, (await Assert.ThrowsAsync<PlatformServiceException>(() => second.Task)).Status);
        Assert.Equal(PlatformServiceStatus.Shutdown, Assert.Throws<PlatformServiceException>(() => state.Begin(host, default)).Status);
        Assert.False(state.Busy);
    }

    [Fact]
    public async Task RequestCodesExhaustRatherThanWrapIntoStaleCallbacks()
    {
        var state = new NativeRequestCoordinator<object, int>(8192, 8192); var host = new object();
        var first = state.Begin(host, default);
        state.Complete(host, first.Code, 1);
        await first.Task;
        Assert.Equal(PlatformServiceStatus.Unavailable, Assert.Throws<PlatformServiceException>(() => state.Begin(host, default)).Status);
    }

    [Fact]
    public void PreCanceledRequestDoesNotOccupySlot()
    {
        var state = new NativeRequestCoordinator<object, int>();
        Assert.Throws<OperationCanceledException>(() => state.Begin(new object(), new CancellationToken(true)));
        Assert.False(state.Busy);
    }

    [Theory]
    [InlineData(23, false)][InlineData(25, false)][InlineData(26, true)][InlineData(34, true)]
    public void NotificationChannelBoundary(int api, bool expected) => Assert.Equal(expected, AndroidNotificationPolicy.NeedsChannel(api));

    [Theory]
    [InlineData(23, PlatformPermissionStatus.NotDeclared, true, true, false, PlatformServiceStatus.Success)]
    [InlineData(32, PlatformPermissionStatus.Denied, true, true, false, PlatformServiceStatus.Success)]
    [InlineData(33, PlatformPermissionStatus.NotDeclared, true, true, false, PlatformServiceStatus.NotDeclared)]
    [InlineData(34, PlatformPermissionStatus.Denied, true, true, false, PlatformServiceStatus.PermissionDenied)]
    [InlineData(34, PlatformPermissionStatus.Granted, true, true, false, PlatformServiceStatus.Success)]
    [InlineData(34, PlatformPermissionStatus.Granted, false, true, false, PlatformServiceStatus.PermissionDenied)]
    [InlineData(26, PlatformPermissionStatus.Granted, true, false, false, PlatformServiceStatus.PermissionDenied)]
    [InlineData(34, PlatformPermissionStatus.Granted, true, true, true, PlatformServiceStatus.Shutdown)]
    public void NotificationAvailabilityHasNoActivityDependency(int api, PlatformPermissionStatus permission,
        bool enabled, bool channel, bool shutdown, PlatformServiceStatus expected)
        => Assert.Equal(expected, AndroidNotificationPolicy.Evaluate(api, permission, enabled, channel, shutdown));
}
