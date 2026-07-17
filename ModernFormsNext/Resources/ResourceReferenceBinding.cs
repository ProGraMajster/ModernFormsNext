using System.Reflection;

namespace ModernFormsNext;

/// <summary>
/// Connects one CLR property to one resource key without retaining the target control globally.
/// </summary>
internal sealed class ResourceReferenceBinding : IResourceChangeListener, IDisposable
{
    private readonly WeakReference<Control> targetReference;
    private readonly PropertyInfo property;
    private readonly object? fallbackValue;
    private object? lastResolvedValue;
    private bool hasLastResolvedValue;
    private bool isUsingResource;
    private bool disposed;

    public ResourceReferenceBinding(
        Control target,
        PropertyInfo property,
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

        ApplyValue(target, Fallback, throwOnFailure: false);
        isUsingResource = false;
        hasLastResolvedValue = false;
    }

    public void OnResourceChanged(ResourceDictionary source, object key)
    {
        if (disposed)
            return;

        if (!targetReference.TryGetTarget(out var target))
        {
            Dispose();
            return;
        }

        if (target.IsResourceScopeInHierarchy(source))
            ApplyEffectiveValue(target, throwOnFailure: false);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        ResourceChangeHub.Unsubscribe(ResourceKey, this);
    }

    private void ApplyEffectiveValue(Control target, bool throwOnFailure)
    {
        if (target.TryFindResource(ResourceKey, out var value))
        {
            if (isUsingResource && hasLastResolvedValue && Equals(lastResolvedValue, value))
                return;

            if (ApplyValue(target, value, throwOnFailure))
            {
                lastResolvedValue = value;
                hasLastResolvedValue = true;
                isUsingResource = true;
            }
            else if (isUsingResource)
            {
                ApplyValue(target, Fallback, throwOnFailure: false);
                isUsingResource = false;
                hasLastResolvedValue = false;
            }

            return;
        }

        if (!isUsingResource)
            return;

        ApplyValue(target, Fallback, throwOnFailure: false);
        isUsingResource = false;
        hasLastResolvedValue = false;
    }

    private bool ApplyValue(Control target, object? value, bool throwOnFailure)
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
            if (Equals(property.GetValue(target), value))
                return true;

            property.SetValue(target, value);
            return true;
        }
        catch (Exception exception)
        {
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
