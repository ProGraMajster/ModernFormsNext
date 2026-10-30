using System.Reflection;

namespace ModernFormsNext;

/// <summary>
/// Connects one CLR property to one resource key without retaining the target control globally.
/// </summary>
internal sealed class ResourceReferenceBinding : IResourceChangeListener, IDisposable
{
    private readonly WeakReference<Control> targetReference;
    private readonly ResourcePropertyAccessor property;
    private readonly object? fallbackValue;
    private object? lastResolvedValue;
    private bool hasLastResolvedValue;
    private bool isUsingResource;
    private bool disposed;
    private long revision;

    public ResourceReferenceBinding(
        Control target,
        ResourcePropertyAccessor property,
        object resourceKey,
        object? fallbackValue)
    {
        targetReference = new WeakReference<Control>(target);
        this.property = property;
        ResourceKey = resourceKey;
        this.fallbackValue = fallbackValue;
        ResourceChangeHub.Subscribe(resourceKey, this);
    }

    public string PropertyName => property.Name;

    public object ResourceKey { get; }

    public object? Fallback => fallbackValue;

    public void ApplyInitialValue()
    {
        if (!targetReference.TryGetTarget(out var target))
            return;

        ApplyEffectiveValue(target, throwOnFailure: true);
    }

    public void Refresh()
    {
        if (disposed)
            return;

        if (!targetReference.TryGetTarget(out var target))
        {
            Dispose();
            return;
        }

        ApplyEffectiveValue(target, throwOnFailure: false);
    }

    public void RestoreFallback()
    {
        if (disposed || !targetReference.TryGetTarget(out var target))
            return;

        // This path is used only after clearing/rejecting the reference. Detach before invoking
        // the fallback setter so a nested resource mutation cannot reactivate the removed binding.
        ResourceChangeHub.Unsubscribe(ResourceKey, this);
        long current = ++revision;
        ApplyValue(target, Fallback, throwOnFailure: false, current);
        if (!IsCurrent(current))
            return;
        isUsingResource = false;
        hasLastResolvedValue = false;
    }

    public bool OnResourceChanged(ResourceDictionary source, object key)
    {
        if (disposed)
            return false;

        if (!targetReference.TryGetTarget(out var target))
        {
            Dispose();
            return false;
        }

        if (target.IsResourceScopeInHierarchy(source))
        {
            ApplyEffectiveValue(target, throwOnFailure: false);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        revision++;
        ResourceChangeHub.Unsubscribe(ResourceKey, this);
    }

    private void ApplyEffectiveValue(Control target, bool throwOnFailure)
    {
        long current = ++revision;
        if (target.TryFindResource(ResourceKey, out var value))
        {
            if (isUsingResource && hasLastResolvedValue && Equals(lastResolvedValue, value))
                return;

            bool applied = ApplyValue(target, value, throwOnFailure, current);
            if (!IsCurrent(current))
                return;
            if (applied)
            {
                lastResolvedValue = value;
                hasLastResolvedValue = true;
                isUsingResource = true;
            }
            else if (isUsingResource)
            {
                ApplyValue(target, Fallback, throwOnFailure: false, current);
                if (!IsCurrent(current))
                    return;
                isUsingResource = false;
                hasLastResolvedValue = false;
            }

            return;
        }

        if (!isUsingResource)
            return;

        ApplyValue(target, Fallback, throwOnFailure: false, current);
        if (!IsCurrent(current))
            return;
        isUsingResource = false;
        hasLastResolvedValue = false;
    }

    private bool IsCurrent(long current) => !disposed && revision == current;

    private bool ApplyValue(Control target, object? value, bool throwOnFailure, long current)
    {
        if (!CanAssign(property.PropertyType, value))
        {
            var actualType = value?.GetType();
            var exception = new InvalidOperationException(
                $"Resource '{ResourceKey}' has type '{actualType?.FullName ?? "null"}', which cannot be assigned " +
                $"to '{target.GetType().FullName}.{property.Name}' of type '{property.PropertyType.FullName}'.");

            if (throwOnFailure)
                throw exception;

            target.OnResourceReferenceFailed(new ResourceReferenceErrorEventArgs(
                ResourceKey,
                property.Name,
                property.PropertyType,
                actualType,
                exception));
            return false;
        }

        try
        {
            object? existing = property.GetValue(target);
            // Accessors and diagnostic handlers are application code. A nested update or replacement
            // owns the property from that point; the older frame must not write or restore fallback.
            if (!IsCurrent(current))
                return false;
            if (Equals(existing, value))
                return true;

            property.SetValue(target, value);
            return true;
        }
        catch (Exception exception)
        {
            if (!IsCurrent(current))
                return false;
            var effectiveException = exception is TargetInvocationException { InnerException: not null }
                ? exception.InnerException
                : exception;

            if (throwOnFailure)
            {
                throw new InvalidOperationException(
                    $"Resource '{ResourceKey}' could not update " +
                    $"'{target.GetType().FullName}.{property.Name}'.",
                    effectiveException);
            }

            target.OnResourceReferenceFailed(new ResourceReferenceErrorEventArgs(
                ResourceKey,
                property.Name,
                property.PropertyType,
                value?.GetType(),
                effectiveException));
            return false;
        }
    }

    private static bool CanAssign(Type targetType, object? value)
    {
        if (value is null)
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;

        if (targetType.IsInstanceOfType(value))
            return true;

        Type? nullableType = Nullable.GetUnderlyingType(targetType);
        return nullableType?.IsInstanceOfType(value) == true;
    }
}
