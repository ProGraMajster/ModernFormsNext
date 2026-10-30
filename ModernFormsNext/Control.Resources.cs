using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ModernFormsNext.Layout;

namespace ModernFormsNext;

public partial class Control
{
    private static readonly int s_resourcesProperty = PropertyStore.CreateKey();
    private static readonly int s_resourceReferencesProperty = PropertyStore.CreateKey();
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo> ResourceProperties = [];

    /// <summary>
    /// Gets the resources defined directly on this control.
    /// </summary>
    /// <remarks>
    /// Resource lookup starts here, then walks parent controls, the owning window,
    /// <see cref="Application.Resources"/>, and finally manager-owned theme resources. The
    /// dictionary is created lazily so controls that do not define resources keep the normal
    /// lightweight storage profile.
    /// </remarks>
    public ResourceDictionary Resources
    {
        get
        {
            if (!Properties.TryGetValue(s_resourcesProperty, out ResourceDictionary? resources))
                resources = Properties.AddValue(s_resourcesProperty, new ResourceDictionary());

            return resources!;
        }
    }

    /// <summary>
    /// Occurs when a runtime resource value cannot be assigned to its referenced CLR property.
    /// </summary>
    /// <remarks>
    /// Initial type mismatches are rejected by <see cref="SetResourceReference(string, object)"/>.
    /// This event reports later incompatible resource replacements or exceptions raised by a
    /// property setter. The control restores the value captured before the reference was created.
    /// </remarks>
    public event EventHandler<ResourceReferenceErrorEventArgs>? ResourceReferenceFailed;

    /// <summary>
    /// Creates or replaces a dynamic reference from a public writable CLR property to a resource key.
    /// </summary>
    /// <param name="propertyName">
    /// The case-sensitive name of a public writable instance property on this control.
    /// </param>
    /// <param name="resourceKey">The non-null resource key to resolve.</param>
    /// <remarks>
    /// <para>
    /// The current property value is captured as the final fallback. When the resource changes, the
    /// normal property setter is invoked; the setter remains responsible for the appropriate repaint
    /// or layout request. If no scope contains the key, the captured value is used.
    /// </para>
    /// <para>
    /// Assigning the property directly does not remove the reference. Call
    /// <see cref="ClearResourceReference(string)"/> before taking manual ownership of its value.
    /// Resource updates for live controls must occur on the owning UI thread.
    /// </para>
    /// <para>
    /// This name-based overload requires the runtime control's public property getter and setter
    /// metadata to be preserved. Trimmed applications should use the typed
    /// <see cref="ResourceProperty{TControl, TValue}"/> overload instead; otherwise trim analysis
    /// reports IL2026. No dynamic-code generation is used by either overload.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;
    /// button.SetResourceReference(nameof(Control.BackColor), "Button.Primary.Background");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when the property name is empty, missing, indexed, static, or not publicly writable.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="propertyName"/> or <paramref name="resourceKey"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the initially resolved resource value is incompatible with the property type or
    /// the property setter rejects it.
    /// </exception>
    [RequiresUnreferencedCode("CLR property names require preserved getter/setter metadata for public properties on the runtime control type. Use the ResourceProperty<TControl, TValue> overload for trimming/AOT.")]
    public void SetResourceReference(string propertyName, object resourceKey)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        ArgumentNullException.ThrowIfNull(resourceKey);
        if (string.IsNullOrWhiteSpace(propertyName))
            throw new ArgumentException("A resource target property name cannot be empty.", nameof(propertyName));

        SetResourceReferenceCore(new ReflectedResourcePropertyAccessor(GetResourceProperty(GetType(), propertyName)), resourceKey);
    }

    /// <summary>Creates a dynamic resource reference using statically referenced CLR accessors.</summary>
    /// <typeparam name="TControl">The descriptor's declaring control type.</typeparam>
    /// <typeparam name="TValue">The exact property value type.</typeparam>
    /// <param name="property">The reflection-free descriptor; inherited properties are supported.</param>
    /// <param name="resourceKey">The non-null resource key.</param>
    /// <remarks>
    /// Uses the same scopes, fallback, weak lifetime and synchronous UI-thread setter rules as the
    /// name-based overload. Use this overload in trimmed/AOT applications. Clearing by
    /// <see cref="ResourceProperty{TControl, TValue}.Name"/> restores the original fallback.
    /// Delegates are trusted application code and are never evaluated by Designer metadata readers.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The descriptor or key is null.</exception>
    /// <exception cref="ArgumentException">This control is not assignable to <typeparamref name="TControl"/>.</exception>
    /// <exception cref="InvalidOperationException">The initial value or an accessor is invalid.</exception>
    public void SetResourceReference<TControl, TValue>(ResourceProperty<TControl, TValue> property, object resourceKey)
        where TControl : Control
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(resourceKey);
        if (this is not TControl)
            throw new ArgumentException($"Resource property '{property.Name}' requires control type '{typeof(TControl).FullName}'.", nameof(property));

        SetResourceReferenceCore(property.Accessor, resourceKey);
    }

    private void SetResourceReferenceCore(ResourcePropertyAccessor property, object resourceKey)
    {
        ObjectDisposedException.ThrowIf(IsDisposed || Disposing, this);
        Dictionary<string, ResourceReferenceBinding> references = GetOrCreateResourceReferences();

        object? fallbackValue;
        if (references.Remove(property.Name, out var previous))
        {
            fallbackValue = previous.Fallback;
            previous.Dispose();
        }
        else
        {
            fallbackValue = property.GetValue(this);
            if (IsDisposed || Disposing)
                return;
            // A getter may install a newer reference. Its choice wins over this older request.
            if (references.ContainsKey(property.Name))
                return;
        }

        var reference = new ResourceReferenceBinding(this, property, resourceKey, fallbackValue);
        references.Add(property.Name, reference);

        try
        {
            reference.ApplyInitialValue();
        }
        catch
        {
            try
            {
                if (references.TryGetValue(property.Name, out var current) && ReferenceEquals(current, reference))
                {
                    references.Remove(property.Name);
                    reference.RestoreFallback();
                }
            }
            finally { reference.Dispose(); }
            throw;
        }
    }

    /// <summary>
    /// Removes a dynamic resource reference and restores the property value captured when the
    /// reference was first created.
    /// </summary>
    /// <param name="propertyName">The case-sensitive target property name.</param>
    /// <returns><see langword="true"/> when a reference was removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyName"/> is <see langword="null"/>.</exception>
    public bool ClearResourceReference(string propertyName)
    {
        ArgumentNullException.ThrowIfNull(propertyName);
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references) ||
            !references!.Remove(propertyName, out var reference))
        {
            return false;
        }

        try { reference.RestoreFallback(); }
        finally
        {
            reference.Dispose();
            if (references.Count == 0)
                Properties.RemoveValue(s_resourceReferencesProperty);
        }

        return true;
    }

    /// <summary>
    /// Searches this control, its parents, owning window, application resources, and theme-resource
    /// fallback for the specified key.
    /// </summary>
    /// <param name="resourceKey">The non-null resource key to find.</param>
    /// <param name="value">Receives the nearest resource value when found.</param>
    /// <returns><see langword="true"/> when a resource was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceKey"/> is <see langword="null"/>.</exception>
    public bool TryFindResource(object resourceKey, out object? value)
    {
        ArgumentNullException.ThrowIfNull(resourceKey);

        for (Control? current = this; current is not null; current = current.Parent)
        {
            if (current.ResourcesInternal?.TryGetValue(resourceKey, out value) == true)
                return true;
        }

        if (FindWindow()?.ResourcesInternal?.TryGetValue(resourceKey, out value) == true)
            return true;

        if (Application.Resources.TryGetValue(resourceKey, out value))
            return true;

        return Application.ThemeResourcesInternal.TryGetValue(resourceKey, out value);
    }

    /// <summary>
    /// Raises <see cref="ResourceReferenceFailed"/>.
    /// </summary>
    /// <param name="e">Information about the rejected resource value.</param>
    /// <remarks>
    /// Derived controls that override this method should call the base implementation so registered
    /// handlers receive the diagnostic.
    /// </remarks>
    protected internal virtual void OnResourceReferenceFailed(ResourceReferenceErrorEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ResourceReferenceFailed?.Invoke(this, e);
    }

    internal ResourceDictionary? ResourcesInternal
        => Properties.TryGetValue(s_resourcesProperty, out ResourceDictionary? resources) ? resources : null;

    internal bool IsResourceScopeInHierarchy(ResourceDictionary source)
    {
        for (Control? current = this; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current.ResourcesInternal, source))
                return true;
        }

        if (ReferenceEquals(FindWindow()?.ResourcesInternal, source))
            return true;

        return ReferenceEquals(Application.Resources, source) ||
            ReferenceEquals(Application.ThemeResourcesInternal, source);
    }

    internal void RefreshResourceBindingsForSubtree()
    {
        if (Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            foreach (var reference in references!.Values.ToArray())
                reference.Refresh();
        }

        if (Properties.GetObject(s_controlsCollectionProperty) is ControlCollection children)
        {
            foreach (Control child in children)
                child.RefreshResourceBindingsForSubtree();
        }
    }

    [RequiresUnreferencedCode("Runtime property names require preserved public property metadata. Use ResourceProperty<TControl, TValue> instead.")]
    private static PropertyInfo GetResourceProperty(Type controlType, string propertyName)
    {
        return ResourceProperties.GetOrAdd((controlType, propertyName), static key =>
        {
            PropertyInfo? property = key.Type.GetProperty(
                key.PropertyName,
                BindingFlags.Instance | BindingFlags.Public);

            if (property is null)
            {
                throw new ArgumentException(
                    $"Public property '{key.PropertyName}' was not found on control type '{key.Type.FullName}'. " +
                    "In trimmed/AOT applications use ResourceProperty<TControl, TValue> or explicitly preserve the public getter/setter metadata.",
                    "propertyName");
            }

            if (property.GetIndexParameters().Length != 0 || property.GetMethod is null || property.SetMethod?.IsPublic != true)
            {
                throw new ArgumentException(
                    $"Property '{key.Type.FullName}.{key.PropertyName}' must be a readable public writable non-indexed instance property.",
                    "propertyName");
            }

            return property;
        });
    }

    private Dictionary<string, ResourceReferenceBinding> GetOrCreateResourceReferences()
    {
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            references = Properties.AddValue(
                s_resourceReferencesProperty,
                new Dictionary<string, ResourceReferenceBinding>(StringComparer.Ordinal));
        }

        return references!;
    }

    private void DisposeResourceReferences()
    {
        if (!Properties.TryGetValue(
                s_resourceReferencesProperty,
                out Dictionary<string, ResourceReferenceBinding>? references))
        {
            return;
        }

        foreach (var reference in references!.Values)
            reference.Dispose();

        references.Clear();
        Properties.RemoveValue(s_resourceReferencesProperty);
    }
}
