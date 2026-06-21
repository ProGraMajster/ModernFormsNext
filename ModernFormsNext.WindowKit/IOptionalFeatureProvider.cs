using System;
using System.Diagnostics.CodeAnalysis;

namespace ModernFormsNext.WindowKit.Platform;

/// <summary>
/// Exposes optional backend features without forcing every implementation to support every contract.
/// </summary>
/// <remarks>
/// Use this interface to query platform-specific or backend-specific capabilities, such as
/// storage dialogs, while keeping the shared top-level abstractions platform-neutral.
/// </remarks>
public interface IOptionalFeatureProvider
{
    /// <summary>
    /// Queries for an optional feature.
    /// </summary>
    /// <param name="featureType">The feature contract type to query.</param>
    /// <returns>The feature implementation, or <see langword="null"/> when it is not available.</returns>
    public object? TryGetFeature(Type featureType);
}

/// <summary>
/// Provides strongly typed helpers for querying optional platform features.
/// </summary>
public static class OptionalFeatureProviderExtensions
{
    /// <summary>
    /// Queries for an optional feature by its contract type.
    /// </summary>
    /// <typeparam name="T">The feature contract type.</typeparam>
    /// <param name="provider">The optional feature provider to query.</param>
    /// <returns>The feature implementation, or <see langword="null"/> when it is not available.</returns>
    public static T? TryGetFeature<T>(this IOptionalFeatureProvider provider) where T : class =>
        (T?)provider.TryGetFeature(typeof(T));

    /// <summary>
    /// Queries for an optional feature by its contract type.
    /// </summary>
    /// <typeparam name="T">The feature contract type.</typeparam>
    /// <param name="provider">The optional feature provider to query.</param>
    /// <param name="rv">Receives the feature implementation when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the feature is available; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetFeature<T>(this IOptionalFeatureProvider provider, [MaybeNullWhen(false)] out T rv)
        where T : class
    {
        rv = provider.TryGetFeature<T>();
        return rv != null;
    }
}
