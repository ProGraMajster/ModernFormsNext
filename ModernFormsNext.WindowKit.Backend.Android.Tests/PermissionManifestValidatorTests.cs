using ModernFormsNext.WindowKit.Backend.Android.Permissions;
using ModernFormsNext.WindowKit.Platform.Permissions;

namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

public sealed class PermissionManifestValidatorTests
{
    [Fact]
    public void MissingCameraDeclarationReturnsExactPermissionName()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.Camera, 35);

        var result = PermissionManifestValidator.Validate(
            definition,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.False(result.IsDeclared);
        Assert.Equal(AndroidPermissionMapper.Camera, result.MissingPermission);
    }

    [Fact]
    public void ForegroundLocationAcceptsOneAlternativeDeclaration()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationWhenInUse, 35);
        var declarations = new HashSet<string>(StringComparer.Ordinal)
        {
            AndroidPermissionMapper.AccessCoarseLocation
        };

        var result = PermissionManifestValidator.Validate(definition, declarations);

        Assert.True(result.IsDeclared);
        Assert.Null(result.MissingPermission);
    }

    [Fact]
    public void BackgroundLocationRequiresBackgroundAndForegroundDeclarations()
    {
        var definition = AndroidPermissionMapper.Map(PlatformPermission.LocationAlways, 35);
        var declarations = new HashSet<string>(StringComparer.Ordinal)
        {
            AndroidPermissionMapper.AccessBackgroundLocation
        };

        var result = PermissionManifestValidator.Validate(definition, declarations);

        Assert.False(result.IsDeclared);
        Assert.Equal(AndroidPermissionMapper.AccessCoarseLocation, result.MissingPermission);
    }
}
