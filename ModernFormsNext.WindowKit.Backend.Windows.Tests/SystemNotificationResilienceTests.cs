using System.Runtime.InteropServices;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class SystemNotificationResilienceTests
{
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(32)] [InlineData(100)] [InlineData(1000)]
    public void CompletedAndRemovedShellIdentitiesAreReclaimed(int operations)
    {
        var released = 0;
        using var leases = new ShellNotificationLeases<object>(_ => released++);
        for (var index = 0; index < operations; index++)
        {
            var key = new SystemNotificationKey("item-" + index, "test"); var native = new object();
            Assert.True(leases.CanAllocate(key)); leases.Replace(key, native);
            Assert.InRange(leases.Count, 1, 32);
            Assert.True(leases.Remove(key, native)); Assert.False(leases.Remove(key, native));
        }
        Assert.Equal(operations, released); Assert.Equal(0, leases.Count);
    }

    [Fact]
    public void LostCallbacksExpireBoundedLeasesWithoutReleasingEarly()
    {
        var time = new Clock(); var released = 0;
        using var leases = new ShellNotificationLeases<object>(_ => released++, time);
        for (var i = 0; i < 32; i++) leases.Replace(new("item-" + i, "test"), new());
        for (var i = 32; i < 1000; i++) Assert.False(leases.CanAllocate(new("item-" + i, "test")));
        Assert.Equal(32, leases.Count); Assert.Equal(0, released);
        time.Seconds = 119; leases.Expire(); Assert.Equal(32, leases.Count);
        time.Seconds = 120; leases.Expire(); Assert.Equal(0, leases.Count); Assert.Equal(32, released);
        Assert.True(leases.CanAllocate(new("next", "test")));
    }

    [Fact]
    public void ReplacementUsesOneLogicalIdentityAndLateOldCallbackCannotRemoveNewLease()
    {
        var key = new SystemNotificationKey("progress", "test"); var released = 0;
        var old = new object(); var current = new object();
        var leases = new ShellNotificationLeases<object>(_ => released++);
        leases.Replace(key, old); leases.Replace(key, current);
        Assert.Equal(1, leases.Count); Assert.Equal(1, released);
        Assert.False(leases.Remove(key, old)); Assert.True(leases.IsCurrent(key, current));
        leases.Dispose(); leases.Dispose(); Assert.Equal(2, released); Assert.Equal(0, leases.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ClassicElementNotFoundFallsBackOnceAndNeverRetriesBrokenProvider(bool duringInitialization)
    {
        var classic = new Fake { FailInitialization = duringInitialization, FailStatus = !duringInitialization };
        var shell = new Fake(); var created = 0;
        var chain = new WindowsNotificationProviderChain(
            [("Classic", () => { created++; return classic; }), ("Shell", () => shell)], action => action(), () => true);
        chain.Initialize(new(), _ => { }, _ => { });
        for (var i = 0; i < 1000; i++) Assert.Equal(SystemNotificationAvailability.Ready, (await chain.GetStatusAsync(default)).Availability);
        Assert.Equal(1, created); Assert.Equal(1000, shell.StatusCalls);
        Assert.Equal(duringInitialization ? 0 : 1, classic.StatusCalls);
        Assert.Contains("0x80070490", chain.Capabilities.Reason);
        Assert.Equal(1, classic.Disposals);
        await chain.DisposeAsync(); Assert.Equal(1, shell.Disposals);
    }

    [Fact]
    public void ThousandProgressReplacementsKeepOneLogicalLeaseAndIgnoreEveryLateCallback()
    {
        var key = new SystemNotificationKey("progress", "test"); var released = 0;
        var leases = new ShellNotificationLeases<object>(_ => released++);
        var current = new object(); leases.Replace(key, current);
        for (var i = 0; i < 1000; i++)
        {
            var old = current; current = new object(); leases.Replace(key, current);
            Assert.False(leases.Remove(key, old)); Assert.Equal(1, leases.Count);
        }
        Assert.Equal(1000, released);
        Assert.True(leases.Remove(key, current)); leases.Dispose();
        Assert.Equal(1001, released);
    }

    private sealed class Clock : TimeProvider
    {
        public long Seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
    private sealed class Fake : IWindowsNotificationBackend
    {
        public bool FailInitialization, FailStatus; public int StatusCalls, Disposals;
        public SystemNotificationCapabilities Capabilities => new("fake", SystemNotificationFeatures.Basic);
        public void Initialize(WindowsNotificationRegistrationOptions options, Action<SystemNotificationActivation> activated, Action<SystemNotificationChange> changed)
        { if (FailInitialization) { Disposals++; throw new COMException("synthetic", unchecked((int)0x80070490)); } }
        public Task<SystemNotificationAccess> GetStatusAsync(CancellationToken token)
        { StatusCalls++; return FailStatus ? Task.FromException<SystemNotificationAccess>(new COMException("synthetic", unchecked((int)0x80070490))) : Task.FromResult(new SystemNotificationAccess(SystemNotificationAvailability.Ready)); }
        public Task<SystemNotificationResult> ShowAsync(SystemNotification n, CancellationToken token) => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted));
        public Task<SystemNotificationResult> UpdateAsync(SystemNotificationKey key, SystemNotificationProgress p, CancellationToken token) => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));
        public Task<SystemNotificationResult> RemoveAsync(SystemNotificationKey? key, string? tag, string? group, CancellationToken token) => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted));
        public Task<SystemNotificationResult> DismissHistoryAsync(SystemNotificationReference reference, CancellationToken token) => Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Unsupported));
        public Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<SystemNotificationHistoryEntry>>([]);
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
