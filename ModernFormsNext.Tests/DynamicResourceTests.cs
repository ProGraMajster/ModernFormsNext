using System.Reflection;
using System.Runtime.CompilerServices;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class DynamicResourceTests
{
    [Fact]
    public void FindsApplicationResource()
    {
        object key = NewKey();
        Application.Resources[key] = "application";

        try
        {
            using var control = new ProbeControl();

            Assert.True(control.TryFindResource(key, out object? value));
            Assert.Equal("application", value);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    [Fact]
    public void WindowResourceOverridesApplicationResource()
    {
        object key = NewKey();
        Application.Resources[key] = 10;

        try
        {
            using var window = new TestWindow();
            using var control = new ProbeControl();
            window.Resources[key] = 20;
            window.Controls.Add(control);

            Assert.True(control.TryFindResource(key, out object? value));
            Assert.Equal(20, value);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    [Fact]
    public void NearestControlResourceOverridesAncestors()
    {
        object key = NewKey();
        using var parent = new Control();
        using var child = new ProbeControl();
        parent.Resources[key] = 20;
        child.Resources[key] = 30;
        parent.Controls.Add(child);

        Assert.True(child.TryFindResource(key, out object? value));
        Assert.Equal(30, value);
    }

    [Fact]
    public void RuntimeChangeAutomaticallyUpdatesTargetProperty()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 5 };
        control.Resources[key] = 10;
        control.SetResourceReference(nameof(ProbeControl.Value), key);
        control.ResetObservations();

        control.Resources[key] = 25;

        Assert.Equal(25, control.Value);
        Assert.Equal(1, control.SetterCalls);
    }

    [Fact]
    public void InitialReferenceDoesNotInvokeSetterWhenEffectiveValueIsUnchanged()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 10 };
        control.Resources[key] = 10;
        control.ResetObservations();

        control.SetResourceReference(nameof(ProbeControl.Value), key);

        Assert.Equal(10, control.Value);
        Assert.Equal(0, control.SetterCalls);
    }

    [Fact]
    public void RemovingOverrideFallsBackToApplicationResource()
    {
        object key = NewKey();
        Application.Resources[key] = 10;

        try
        {
            using var control = new ProbeControl { Value = 5 };
            control.Resources[key] = 20;
            control.SetResourceReference(nameof(ProbeControl.Value), key);

            Assert.True(control.Resources.Remove(key));

            Assert.Equal(10, control.Value);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    [Fact]
    public void MissingKeyPreservesCapturedFallback()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 7 };

        control.SetResourceReference(nameof(ProbeControl.Value), key);

        Assert.False(control.TryFindResource(key, out _));
        Assert.Equal(7, control.Value);
    }

    [Fact]
    public void InitialIncompatibleValueIsRejectedAndReferenceIsRemoved()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 7 };
        control.Resources[key] = "not an integer";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => control.SetResourceReference(nameof(ProbeControl.Value), key));
        control.Resources[key] = 20;

        Assert.Contains(nameof(ProbeControl.Value), exception.Message, StringComparison.Ordinal);
        Assert.Equal(7, control.Value);
        Assert.False(control.ClearResourceReference(nameof(ProbeControl.Value)));
    }

    [Fact]
    public void RuntimeIncompatibleValueRestoresFallbackAndReportsDiagnostic()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 7 };
        control.Resources[key] = 10;
        control.SetResourceReference(nameof(ProbeControl.Value), key);
        ResourceReferenceErrorEventArgs? error = null;
        control.ResourceReferenceFailed += (_, e) => error = e;

        control.Resources[key] = "not an integer";

        Assert.Equal(7, control.Value);
        Assert.NotNull(error);
        Assert.Equal(key, error.ResourceKey);
        Assert.Equal(typeof(int), error.ExpectedType);
        Assert.Equal(typeof(string), error.ActualType);
    }

    [Fact]
    public void OnlyReferencesForChangedEffectiveValueInvokeTheirSetters()
    {
        object firstKey = NewKey();
        object secondKey = NewKey();
        using var first = new ProbeControl();
        using var second = new ProbeControl();
        first.Resources[firstKey] = 1;
        second.Resources[secondKey] = 2;
        first.SetResourceReference(nameof(ProbeControl.Value), firstKey);
        second.SetResourceReference(nameof(ProbeControl.Value), secondKey);
        first.ResetObservations();
        second.ResetObservations();

        first.Resources[firstKey] = 3;

        Assert.Equal(1, first.SetterCalls);
        Assert.Equal(0, second.SetterCalls);
    }

    [Fact]
    public void ResourceChangeRunsTargetSetterOnCallingUiThread()
    {
        object key = NewKey();
        using var control = new ProbeControl();
        control.Resources[key] = 1;
        control.SetResourceReference(nameof(ProbeControl.Value), key);
        int uiThread = Environment.CurrentManagedThreadId;

        control.Resources[key] = 2;

        Assert.Equal(uiThread, control.LastSetterThread);
    }

    [Fact]
    public void RemovingControlFromTreeDropsAncestorScope()
    {
        object key = NewKey();
        Application.Resources[key] = 5;

        try
        {
            using var parent = new Control();
            using var child = new ProbeControl();
            parent.Resources[key] = 10;
            parent.Controls.Add(child);
            child.SetResourceReference(nameof(ProbeControl.Value), key);
            Assert.Equal(10, child.Value);

            parent.Controls.Remove(child);
            parent.Resources[key] = 20;

            Assert.Null(child.Parent);
            Assert.Equal(5, child.Value);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    [Fact]
    public void ClearResourceReferenceRestoresCapturedValue()
    {
        object key = NewKey();
        using var control = new ProbeControl { Value = 7 };
        control.Resources[key] = 10;
        control.SetResourceReference(nameof(ProbeControl.Value), key);

        Assert.True(control.ClearResourceReference(nameof(ProbeControl.Value)));
        control.Resources[key] = 20;

        Assert.Equal(7, control.Value);
    }

    [Fact]
    public void WeakSubscriptionDoesNotKeepDetachedControlAlive()
    {
        object key = NewKey();
        Application.Resources[key] = 1;

        try
        {
            WeakReference reference = CreateCollectibleResourceBoundControl(key);

            CollectGarbage();
            Application.Resources[key] = 2;

            Assert.False(reference.IsAlive);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    private static object NewKey() => $"DynamicResourceTests.{Guid.NewGuid():N}";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleResourceBoundControl(object key)
    {
        var control = new ProbeControl();
        control.SetResourceReference(nameof(ProbeControl.Value), key);
        return new WeakReference(control);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class ProbeControl : Control
    {
        private int value;

        public int Value
        {
            get => value;
            set
            {
                this.value = value;
                SetterCalls++;
                LastSetterThread = Environment.CurrentManagedThreadId;
            }
        }

        public int SetterCalls { get; private set; }

        public int LastSetterThread { get; private set; }

        public void ResetObservations()
        {
            SetterCalls = 0;
            LastSetterThread = 0;
        }
    }

    private sealed class TestWindow : WindowBase
    {
        public TestWindow()
            : base(DispatchProxy.Create<IWindowBaseImpl, WindowImplProxy>())
        {
        }
    }

    private class WindowImplProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
