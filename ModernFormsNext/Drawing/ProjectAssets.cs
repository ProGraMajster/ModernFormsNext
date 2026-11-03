using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace ModernFormsNext.Drawing;

/// <summary>Opens project assets and assigns images using portable identifiers.</summary>
public static class ProjectAssets
{
    private static readonly ConditionalWeakTable<Control, ImageLifetime> Images = new();

    /// <summary>Opens an embedded or copy-output project asset for reading.</summary>
    /// <param name="assetId">The exact project-relative path under Assets/Embedded or Assets/Content.</param>
    /// <param name="resourceAssembly">The assembly containing the embedded assets.</param>
    /// <returns>A readable stream. The caller must dispose it.</returns>
    /// <remarks>Copy-output assets are resolved from <see cref="AppContext.BaseDirectory"/>.</remarks>
    /// <exception cref="ArgumentException">The identifier is invalid.</exception>
    /// <exception cref="IOException">The asset is unavailable or its path is disallowed.</exception>
    public static Stream OpenRead(string assetId, Assembly resourceAssembly)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        if (!ProjectAssetPath.IsValidId(assetId))
            throw new ArgumentException("The asset identifier is not a portable project path.", nameof(assetId));

        return assetId.StartsWith(ProjectAssetPath.EmbeddedPrefix, StringComparison.Ordinal)
            ? resourceAssembly.GetManifestResourceStream(assetId)
                ?? throw new FileNotFoundException("The embedded project asset was not found.", assetId)
            : ProjectImageAsset.OpenLocal(AppContext.BaseDirectory, assetId);
    }

    /// <summary>Loads a static project image and assigns it to a supported control.</summary>
    /// <param name="control">The control receiving and owning the decoded image.</param>
    /// <param name="assetId">The exact project-relative path under Assets/Embedded or Assets/Content.</param>
    /// <param name="resourceAssembly">The assembly containing the embedded assets.</param>
    /// <returns>Whether the image was loaded. An I/O or decoding failure is also traced.</returns>
    /// <remarks>
    /// Call on the UI thread. This method reads synchronously within the image budget.
    /// Reapplying an asset replaces the previous bitmap. The bitmap is released when the
    /// control is disposed. Animated images display their first frame.
    /// </remarks>
    /// <example>
    /// <code>
    /// ProjectAssets.ApplyImage(picture, "Assets/Embedded/logo.png", typeof(MainForm).Assembly);
    /// </code>
    /// </example>
    public static bool ApplyImage(Control control, string assetId, Assembly resourceAssembly)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ObjectDisposedException.ThrowIf(control.IsDisposed || control.Disposing, control);
        if (!SupportsImage(control))
            throw new ArgumentException("The control has no supported image property.", nameof(control));

        SKBitmap bitmap;
        try
        {
            using var stream = OpenRead(assetId, resourceAssembly);
            bitmap = ProjectImageAsset.Decode(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Project image '{0}' is unavailable: {1}", assetId, exception.Message);
            return false;
        }

        var lifetime = Images.GetValue(control, static owner => new ImageLifetime(owner));
        var previous = lifetime.Bitmap;
        // Layout triggered by the setter may dispose the control synchronously.
        // Establish lifetime ownership before notifying the control.
        lifetime.Bitmap = bitmap;
        try
        {
            Assign(control, bitmap);
        }
        finally
        {
            previous?.Dispose();
        }
        return true;
    }

    internal static bool SupportsImage(Control control)
        => control is PictureBox or IHaveTextAndImageAlign or FormTitleBar;

    internal static void Assign(Control control, SKBitmap bitmap)
    {
        switch (control)
        {
            case PictureBox picture: picture.Image = bitmap; break;
            case IHaveTextAndImageAlign textAndImage: textAndImage.Image = bitmap; break;
            case FormTitleBar titleBar: titleBar.Image = bitmap; break;
        }
    }

    private sealed class ImageLifetime
    {
        internal ImageLifetime(Control owner) => owner.Disposed += OnDisposed;

        internal SKBitmap? Bitmap { get; set; }

        private void OnDisposed(object? sender, EventArgs e)
        {
            if (sender is Control owner)
            {
                owner.Disposed -= OnDisposed;
                Images.Remove(owner);
            }
            Bitmap?.Dispose();
            Bitmap = null;
        }
    }
}
