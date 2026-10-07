using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using Xunit;

namespace ModernFormsNext.Testing.Tests;

public sealed class SystemNotificationLifetimeTests
{
    [Fact]
    public async Task DisposeWaitsForInFlightSubmissionAndRejectsQueuedWork()
    {
        var service = new PendingTransport();
        var first = service.ShowAsync(new() { Title = "First" });
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = service.ShowAsync(new() { Title = "Queued" });
        var dispose = service.DisposeAsync().AsTask();
        Assert.False(dispose.IsCompleted);
        service.Release.SetResult();
        Assert.True((await first.WaitAsync(TimeSpan.FromSeconds(5))).IsAccepted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await service.DisposeAsync();
        Assert.Equal(1, service.Submissions);
        Assert.Equal(1, service.Disposals);
    }

    [Fact]
    public async Task CancellingQueuedRequestDoesNotSubmitOrCancelOtherRequest()
    {
        await using var service = new PendingTransport();
        var first = service.ShowAsync(new() { Title = "First" });
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource();
        var queued = service.ShowAsync(new() { Title = "Cancelled" }, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(1, service.Submissions);
        service.Release.SetResult();
        Assert.True((await first.WaitAsync(TimeSpan.FromSeconds(5))).IsAccepted);
    }

    [Theory]
    [InlineData(SystemNotificationAvailability.PermissionDenied, SystemNotificationAvailability.Ready, SystemNotificationStatus.Accepted)]
    [InlineData(SystemNotificationAvailability.Ready, SystemNotificationAvailability.DisabledByUser, SystemNotificationStatus.Disabled)]
    public async Task OperationAccessCanDifferWithoutChangingGlobalPermission(SystemNotificationAvailability global,
        SystemNotificationAvailability operation, SystemNotificationStatus expected)
    {
        await using var service = new OperationAccessTransport(global, operation);
        var notification = new SystemNotification { Id = "operation", Group = "category", Title = "Message", Progress = new() { Value = .1 } };
        Assert.Equal(global, (await service.GetStatusAsync()).Availability);
        Assert.Equal(expected, (await service.ShowAsync(notification)).Status);
        Assert.Equal(expected, (await service.UpdateAsync(new(notification.Id, notification.Group), new() { Value = .2 })).Status);
        Assert.Equal(global, (await service.GetStatusAsync()).Availability);
        Assert.Equal(expected == SystemNotificationStatus.Accepted ? 2 : 0, service.Submissions);
        Assert.True(service.Capabilities.IsSupported);
    }

    // Contract-only transport: exercises per-operation policy without pretending to be an OS backend.
    private sealed class OperationAccessTransport(SystemNotificationAvailability global, SystemNotificationAvailability operation)
        : SystemNotificationServiceBase(action => action())
    {
        internal int Submissions;
        public override SystemNotificationCapabilities Capabilities { get; } = new("operation access",
            SystemNotificationFeatures.Basic | SystemNotificationFeatures.Progress | SystemNotificationFeatures.LiveUpdates);
        protected override Task<SystemNotificationAccess> GetStatusCoreAsync(CancellationToken token) => Task.FromResult(new SystemNotificationAccess(global));
        protected override Task<SystemNotificationAccess> GetShowAccessCoreAsync(SystemNotification notification, CancellationToken token)
        { Assert.Equal("category", notification.Group); return Task.FromResult(new SystemNotificationAccess(operation)); }
        protected override Task<SystemNotificationAccess> GetUpdateAccessCoreAsync(SystemNotificationKey key, CancellationToken token)
        { Assert.Equal("category", key.Group); return Task.FromResult(new SystemNotificationAccess(operation)); }
        protected override Task<SystemNotificationResult> ShowCoreAsync(SystemNotification notification, CancellationToken token)
        { Submissions++; return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted)); }
        protected override Task<SystemNotificationResult> UpdateCoreAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken token)
        { Submissions++; return Task.FromResult(new SystemNotificationResult(SystemNotificationStatus.Accepted)); }
        protected override Task<SystemNotificationResult> RemoveCoreAsync(SystemNotificationKey? key, string? id, string? group, CancellationToken token) => throw new NotSupportedException();
        protected override Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryCoreAsync(CancellationToken token) => throw new NotSupportedException();
        protected override ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;
    }

    // A controlled asynchronous transport makes queue/disposal ordering observable without
    // sleeping, racing the Windows UI thread, or substituting timeouts for event boundaries.
    private sealed class PendingTransport() : SystemNotificationServiceBase(action => action())
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Submissions;
        internal int Disposals;
        public override SystemNotificationCapabilities Capabilities { get; } = new("controlled", SystemNotificationFeatures.Basic);
        protected override async Task<SystemNotificationResult> ShowCoreAsync(SystemNotification notification, CancellationToken token)
        {
            Interlocked.Increment(ref Submissions);
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
            return new(SystemNotificationStatus.Accepted, SystemNotificationValidation.GetKey(notification));
        }
        protected override Task<SystemNotificationResult> UpdateCoreAsync(SystemNotificationKey key, SystemNotificationProgress progress, CancellationToken token) => throw new NotSupportedException();
        protected override Task<SystemNotificationResult> RemoveCoreAsync(SystemNotificationKey? key, string? id, string? group, CancellationToken token) => throw new NotSupportedException();
        protected override Task<IReadOnlyList<SystemNotificationHistoryEntry>> GetHistoryCoreAsync(CancellationToken token) => throw new NotSupportedException();
        protected override ValueTask DisposeCoreAsync() { Interlocked.Increment(ref Disposals); return ValueTask.CompletedTask; }
    }
}
