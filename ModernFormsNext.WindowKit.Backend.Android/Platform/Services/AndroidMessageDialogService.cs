using Android.App;
using Android.Content;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Backend.Android.Windowing;
using ModernFormsNext.WindowKit.Platform;
using ModernFormsNext.WindowKit.Platform.Services;

namespace ModernFormsNext.WindowKit.Backend.Android.Services;

internal sealed class AndroidMessageDialogService(AndroidActivityTracker tracker, AndroidMainThreadDispatcher dispatcher,
    AndroidActivityResultCoordinator requests) : IPlatformMessageDialogService
{
    public Task<int> ShowAsync(IWindowBaseImpl? owner, PlatformMessageDialogRequest request, CancellationToken cancellationToken = default)
    {
        requests.VerifyAccess();
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (requests.IsShutdown) throw new PlatformServiceException(PlatformServiceStatus.Shutdown);
        if (requests.Busy) throw new PlatformServiceException(PlatformServiceStatus.Busy);
        AndroidWindowImpl? window = null;
        Activity? activity;
        if (owner is null) activity = tracker.CurrentActivity;
        else
        {
            window = owner as AndroidWindowImpl;
            activity = window is null ? null : (window.Platform.Host as AndroidActivityHost)?.MessageDialogActivity(window);
        }
        activity = AndroidMessageDialogPlan.RequireHost(activity, requests.IsShutdown, requests.Busy);
        var session = new Session(activity, window, requests, dispatcher, request, cancellationToken);
        var pending = requests.BeginDialog(activity, session.Retire, session.Generation);
        session.Code = pending.Code;
        try { session.Show(activity); }
        catch (Exception exception) { requests.FailDialog(pending.Code, exception); }
        return Result(pending.Task);
    }

    private static async Task<int> Result(Task<AndroidServiceResult> task)
        => (await task.ConfigureAwait(false)).MessageButton;

    // Strong native references are scoped to the pending request only. Completion clears them
    // before publishing its result; delayed Java callbacks carry only an already-retired session.
    private sealed class Session
    {
        private readonly WeakReference<Activity> activity;
        private readonly WeakReference<AndroidWindowImpl>? owner;
        private readonly AndroidActivityResultCoordinator requests;
        private readonly AndroidMainThreadDispatcher dispatcher;
        private readonly PlatformMessageDialogRequest request;
        private readonly AndroidMessageDialogPlan plan;
        private readonly CancellationToken token;
        private readonly long epoch;
        private AlertDialog? dialog;
        private CancellationTokenRegistration cancellation;
        private bool retired;
        internal int Code;
        internal long Generation { get; }

        internal Session(Activity activity, AndroidWindowImpl? owner, AndroidActivityResultCoordinator requests,
            AndroidMainThreadDispatcher dispatcher, PlatformMessageDialogRequest request, CancellationToken token)
        {
            this.activity = new(activity);
            this.owner = owner is null ? null : new(owner);
            Generation = owner?.Platform.Generation ?? 0;
            epoch = owner?.PresentationEpoch ?? 0;
            this.requests = requests; this.dispatcher = dispatcher; this.request = request; this.token = token;
            plan = AndroidMessageDialogPlan.Create(request);
        }

        internal void Show(Activity host)
        {
            token.ThrowIfCancellationRequested();
            using var builder = new AlertDialog.Builder(host);
            builder.SetTitle(request.Title);
            builder.SetMessage(request.Message);
            builder.SetCancelable(plan.Cancel >= 0);
            if (plan.Icon == AndroidMessageIcon.Information) builder.SetIcon(global::Android.Resource.Drawable.IcDialogInfo);
            else if (plan.Icon == AndroidMessageIcon.Alert) builder.SetIcon(global::Android.Resource.Drawable.IcDialogAlert);
            // Android's public "Yes"/"No" strings mean OK/Cancel on some OS versions. Use
            // explicit backend resources for semantic Yes/No/Retry labels.
            string label = request.Buttons switch {
                MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel => host.GetString(Resource.String.mfn_message_yes),
                MessageBoxButtons.RetryCancel => host.GetString(Resource.String.mfn_message_retry),
                _ => host.GetString(global::Android.Resource.String.Ok)
            };
            builder.SetPositiveButton(label, (_, _) => Select(plan.Positive));
            if (plan.Negative >= 0)
                builder.SetNegativeButton(request.Buttons is MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel
                    ? host.GetString(Resource.String.mfn_message_no) : host.GetString(global::Android.Resource.String.Cancel),
                    (_, _) => Select(plan.Negative));
            if (plan.Neutral >= 0)
                builder.SetNeutralButton(global::Android.Resource.String.Cancel, (_, _) => Select(plan.Neutral));
            dialog = builder.Create() ?? throw new InvalidOperationException("AlertDialog creation failed.");
            dialog.CancelEvent += OnCancel;
            dialog.DismissEvent += OnDismiss;
            dialog.SetCanceledOnTouchOutside(plan.Cancel >= 0);
            if (owner?.TryGetTarget(out var window) == true) window.PresentationRetired += OwnerRetired;
            dialog.Show();
            cancellation = token.Register(() => dispatcher.Post(Cancel));
            if (token.IsCancellationRequested) Cancel();
        }

        private void Select(int index)
        {
            if (retired) return;
            if (owner is not null && (!owner.TryGetTarget(out var window) ||
                window.PresentationEpoch != epoch || window.Platform.Generation != Generation))
            { OwnerRetired(); return; }
            if (activity.TryGetTarget(out var host)) requests.CompleteDialog(host, Code, index, Generation);
            else OwnerRetired();
        }
        private void OnCancel(object? sender, EventArgs e) { if (plan.Cancel >= 0) Select(plan.Cancel); }
        private void OnDismiss(object? sender, EventArgs e)
        {
            // An unexplained dismissal is host loss, not an invented affirmative response.
            if (!retired) OwnerRetired();
        }
        private void Cancel()
        {
            if (retired) return;
            if (activity.TryGetTarget(out var host)) requests.CancelDialog(host, Code, token, Generation);
            else OwnerRetired();
        }
        private void OwnerRetired()
        {
            if (!retired) requests.FailDialog(Code, new PlatformServiceException(PlatformServiceStatus.HostLost));
        }
        internal void Retire()
        {
            if (retired) return;
            retired = true;
            cancellation.Dispose();
            if (owner?.TryGetTarget(out var window) == true) window.PresentationRetired -= OwnerRetired;
            var previous = dialog; dialog = null;
            if (previous is null) return;
            // Attempt every cleanup step even if a host has already invalidated a Java peer.
            // Clearing Java listeners also prevents a queued click from retaining this session.
            AndroidWindowingPlatform.Complete([
                () => previous.CancelEvent -= OnCancel,
                () => previous.DismissEvent -= OnDismiss,
                () => previous.SetButton((int)DialogButtonType.Positive, (string?)null, (IDialogInterfaceOnClickListener?)null),
                () => previous.SetButton((int)DialogButtonType.Negative, (string?)null, (IDialogInterfaceOnClickListener?)null),
                () => previous.SetButton((int)DialogButtonType.Neutral, (string?)null, (IDialogInterfaceOnClickListener?)null),
                previous.Dismiss,
                previous.Dispose
            ]);
        }
    }
}
