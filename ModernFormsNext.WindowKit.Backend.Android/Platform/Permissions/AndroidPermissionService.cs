using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.Provider;
using ModernFormsNext.WindowKit.Backend.Android.Dispatching;
using ModernFormsNext.WindowKit.Backend.Android.Lifecycle;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

/// <summary>
/// Implements manifest-aware Android permission checks and serialized runtime requests.
/// </summary>
/// <remarks>
/// The service never adds permissions to the manifest and never opens settings automatically.
/// Runtime requests require a resumed activity. Requested-state markers are stored in private
/// application preferences so a denial with no further rationale can be identified as permanent.
/// </remarks>
public sealed class AndroidPermissionService : IPermissionService
{
    private const string RequestedPreferenceName = "modernformsnext.windowkit.permissions";
    private readonly Context context;
    private readonly AndroidActivityTracker activityTracker;
    private readonly AndroidMainThreadDispatcher dispatcher;
    private readonly AndroidManifestInspector manifestInspector;
    private readonly AndroidPermissionRequestCoordinator coordinator;
    private readonly Action<string>? diagnostics;
    private readonly int sdkVersion;

    internal AndroidPermissionService(
        Context context,
        AndroidActivityTracker activityTracker,
        AndroidMainThreadDispatcher dispatcher,
        TimeSpan requestTimeout,
        Action<string>? diagnostics)
    {
        this.context = context;
        this.activityTracker = activityTracker;
        this.dispatcher = dispatcher;
        this.diagnostics = diagnostics;
        sdkVersion = (int)global::Android.OS.Build.VERSION.SdkInt;
        manifestInspector = new AndroidManifestInspector(context);
        coordinator = new AndroidPermissionRequestCoordinator(activityTracker, dispatcher, requestTimeout);
    }

    /// <inheritdoc/>
    public Task<PlatformPermissionResult> CheckAsync(
        PlatformPermission permission,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Check(permission));
    }

    /// <inheritdoc/>
    public async Task<PlatformPermissionResult> RequestAsync(
        PlatformPermission permission,
        CancellationToken cancellationToken = default)
    {
        var results = await RequestAsync([permission], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlatformPermissionResult>> RequestAsync(
        IEnumerable<PlatformPermission> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        cancellationToken.ThrowIfCancellationRequested();

        var requested = permissions.Distinct().ToArray();
        if (requested.Length == 0)
            return Array.Empty<PlatformPermissionResult>();

        var initialResults = requested.Select(Check).ToArray();
        var runtimePermissions = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < requested.Length; index++)
        {
            var result = initialResults[index];
            var definition = AndroidPermissionMapper.Map(requested[index], sdkVersion);

            if (!AndroidPermissionRequestPlanner.ShouldContinueRequestFlow(result.Status))
                continue;

            if (definition.RequestKind == PlatformPermissionRequestKind.ApplicationSettings)
            {
                initialResults[index] = result with
                {
                    DiagnosticMessage =
                        "Android requires this permission to be changed from application settings. " +
                        "Call OpenApplicationSettingsAsync only after explaining the change to the user."
                };
                continue;
            }

            if (requested[index] == PlatformPermission.LocationAlways && sdkVersion == 29)
            {
                var foreground = Check(PlatformPermission.LocationWhenInUse);
                if (foreground.Status != PlatformPermissionStatus.Granted)
                {
                    initialResults[index] = result with
                    {
                        DiagnosticMessage =
                            "Android background location is a staged flow. Request " +
                            "LocationWhenInUse first, then request LocationAlways separately."
                    };
                    continue;
                }
            }

            foreach (var androidPermission in definition.RuntimePermissions)
                runtimePermissions.Add(androidPermission);
        }

        if (runtimePermissions.Count == 0)
            return initialResults;

        if (activityTracker.CurrentActivity is null)
        {
            const string message =
                "Android cannot show a permission dialog because there is no active Activity. " +
                "Wait until the application is in the foreground and try again.";
            diagnostics?.Invoke(message);
            return initialResults
                .Select(result => result.Status == PlatformPermissionStatus.Denied
                    ? result with { Status = PlatformPermissionStatus.Unknown, DiagnosticMessage = message }
                    : result)
                .ToArray();
        }

        MarkRequested(runtimePermissions);

        try
        {
            await coordinator.RequestAsync(runtimePermissions.ToArray(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            diagnostics?.Invoke(exception.Message);
            return initialResults
                .Select(result => result.Status == PlatformPermissionStatus.Denied
                    ? result with
                    {
                        Status = PlatformPermissionStatus.Unknown,
                        DiagnosticMessage = exception.Message
                    }
                    : result)
                .ToArray();
        }

        return requested.Select(Check).ToArray();
    }

    /// <inheritdoc/>
    public bool ShouldShowRationale(PlatformPermission permission)
    {
        var activity = activityTracker.CurrentActivity
            ?? throw new InvalidOperationException(
                "Android permission rationale cannot be queried because there is no active Activity.");
        var definition = AndroidPermissionMapper.Map(permission, sdkVersion);
        return definition.RuntimePermissions.Any(activity.ShouldShowRequestPermissionRationale);
    }

    /// <inheritdoc/>
    public async Task<bool> OpenApplicationSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var activity = activityTracker.CurrentActivity;
        if (activity is null)
        {
            diagnostics?.Invoke(
                "Android application settings cannot be opened because there is no active Activity.");
            return false;
        }

        return await dispatcher.InvokeAsync(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var packageUri = global::Android.Net.Uri.Parse($"package:{context.PackageName}");
                using var intent = new Intent(Settings.ActionApplicationDetailsSettings, packageUri);
                activity.StartActivity(intent);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal bool HandleRequestPermissionsResult(
        int requestCode,
        string[] permissions,
        Permission[] grantResults)
        => coordinator.HandleRequestPermissionsResult(requestCode, permissions, grantResults);

    private PlatformPermissionResult Check(PlatformPermission permission)
    {
        var definition = AndroidPermissionMapper.Map(permission, sdkVersion);
        if (definition.RequestKind == PlatformPermissionRequestKind.NotSupported)
        {
            return new PlatformPermissionResult(
                permission,
                PlatformPermissionStatus.NotSupported,
                definition.RequestKind,
                $"{permission} is not supported on Android API level {sdkVersion}.");
        }

        var validation = PermissionManifestValidator.Validate(
            definition,
            manifestInspector.GetDeclaredPermissions());
        if (!validation.IsDeclared)
        {
            var message =
                $"{permission} permission is not declared in the Android application manifest. " +
                $"Add {validation.MissingPermission} to the application manifest.";
            diagnostics?.Invoke(message);
            return new PlatformPermissionResult(
                permission,
                PlatformPermissionStatus.NotDeclared,
                definition.RequestKind,
                message);
        }

        if (definition.RuntimePermissions.Count == 0)
        {
            return new PlatformPermissionResult(
                permission,
                PlatformPermissionStatus.Granted,
                definition.RequestKind);
        }

        var activity = activityTracker.CurrentActivity;
        var status = AndroidPermissionStatusEvaluator.Evaluate(
            definition,
            androidPermission => context.CheckSelfPermission(androidPermission) == Permission.Granted,
            androidPermission => activity is not null && WasRequested(androidPermission),
            androidPermission => activity?.ShouldShowRequestPermissionRationale(androidPermission) == true);
        if (status == PlatformPermissionStatus.Granted)
        {
            return new PlatformPermissionResult(
                permission,
                PlatformPermissionStatus.Granted,
                definition.RequestKind);
        }

        return new PlatformPermissionResult(
            permission,
            status,
            definition.RequestKind,
            status == PlatformPermissionStatus.PermanentlyDenied
                ? "Android will not show this permission dialog again. Explain the requirement and " +
                  "offer an explicit action that opens application settings."
                : null);
    }

    private bool WasRequested(string androidPermission)
        => GetPreferences().GetBoolean(androidPermission, false);

    private void MarkRequested(IEnumerable<string> androidPermissions)
    {
        using var editor = GetPreferences().Edit();
        foreach (var androidPermission in androidPermissions)
            editor?.PutBoolean(androidPermission, true);
        editor?.Apply();
    }

    private global::Android.Content.ISharedPreferences GetPreferences()
        => context.GetSharedPreferences(RequestedPreferenceName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Android did not provide private permission preferences.");
}
