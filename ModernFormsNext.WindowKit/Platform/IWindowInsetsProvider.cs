using System;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>Optionally exposes native client-surface occlusion through the existing feature lookup.</summary>
/// <remarks>
/// Backends return this feature from ITopLevelImpl.TryGetFeature. Read and subscribe on the owning
/// UI thread. Changes run on that same thread after CurrentInsets commits. An absent feature means
/// empty insets, as on ordinary Windows client areas. Subscribers must detach when the host closes.
/// </remarks>
public interface IWindowInsetsProvider
{
    /// <summary>Gets the current logical occlusion that has not already been excluded by native fitting.</summary>
    WindowInsets CurrentInsets { get; }

    /// <summary>Occurs on the host UI thread after the current inset snapshot changes.</summary>
    event EventHandler<WindowInsetsChangedEventArgs>? InsetsChanged;
}
