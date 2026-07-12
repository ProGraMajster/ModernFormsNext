using Android.Content;
using Android.Content.PM;

namespace ModernFormsNext.WindowKit.Backend.Android.Permissions;

internal sealed class AndroidManifestInspector
{
    private readonly Context context;

    public AndroidManifestInspector(Context context)
    {
        this.context = context;
    }

    public IReadOnlySet<string> GetDeclaredPermissions()
    {
        var packageManager = context.PackageManager
            ?? throw new InvalidOperationException("Android did not provide a PackageManager.");
        var packageName = context.PackageName
            ?? throw new InvalidOperationException("Android did not provide the application package name.");
        var packageInfo = packageManager.GetPackageInfo(packageName, PackageInfoFlags.Permissions);

        return (packageInfo?.RequestedPermissions ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
    }
}
