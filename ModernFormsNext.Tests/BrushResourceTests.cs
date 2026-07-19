using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModernFormsNext.Drawing;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class BrushResourceTests
{
    [Fact]
    public void ApplicationWindowAndControlScopesResolveNearestBrush()
    {
        object key = NewKey();
        var applicationBrush = new SolidColorBrush(Color.Red);
        var windowBrush = Gradient(Color.Blue, Color.Green);
        var controlBrush = new SolidColorBrush(Color.Yellow);
        Application.Resources[key] = applicationBrush;

        try
        {
            using var window = new TestWindow();
            using var control = new Control();
            window.Controls.Add(control);
            control.SetResourceReference(nameof(Control.BackgroundBrush), key);
            Assert.Same(applicationBrush, control.BackgroundBrush);

            window.Resources[key] = windowBrush;
            Assert.Same(windowBrush, control.BackgroundBrush);

            control.Resources[key] = controlBrush;
            Assert.Same(controlBrush, control.BackgroundBrush);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    [Fact]
    public void ResourceReplacementDetachesOldBrushAndNestedMutationInvalidatesNewBrush()
    {
        object key = NewKey();
        var solid = new SolidColorBrush(Color.Red);
        var gradient = Gradient(Color.Black, Color.White);
        using var control = new InvalidationProbeControl();
        using var surface = new SkiaControlSurface(control);
        control.Resources[key] = solid;
        control.SetResourceReference(nameof(Control.BackgroundBrush), key);
        control.ResetInvalidations();

        control.Resources[key] = gradient;
        control.ResetInvalidations();
        solid.PaintColor = Color.Blue;
        gradient.GradientStops[0].PaintColor = Color.Purple;

        Assert.Same(gradient, control.BackgroundBrush);
        Assert.Equal(1, control.Invalidations);
    }

    [Fact]
    public void RemovingResourceRestoresCapturedColorFallbackAndItsSubscription()
    {
        object key = NewKey();
        var fallback = new SolidColorBrush(Color.Gray);
        var resource = Gradient(Color.Red, Color.Blue);
        using var control = new InvalidationProbeControl { BackgroundBrush = fallback };
        using var surface = new SkiaControlSurface(control);
        control.Resources[key] = resource;
        control.SetResourceReference(nameof(Control.BackgroundBrush), key);

        Assert.True(control.Resources.Remove(key));
        control.ResetInvalidations();
        resource.Opacity = 0.4f;
        fallback.PaintColor = Color.DarkGray;

        Assert.Same(fallback, control.BackgroundBrush);
        Assert.Equal(1, control.Invalidations);
    }

    [Fact]
    public void SharedBrushAcrossTwoPropertiesUsesOneInvalidationAndReferenceCounting()
    {
        var shared = Gradient(Color.Red, Color.Blue);
        using var control = new InvalidationProbeControl
        {
            BackgroundBrush = shared,
            TextBrush = shared
        };
        using var surface = new SkiaControlSurface(control);
        control.ResetInvalidations();

        shared.GradientStops[0].Offset = 0.1f;
        Assert.Equal(1, control.Invalidations);

        control.BackgroundBrush = null;
        control.ResetInvalidations();
        shared.GradientStops[0].Offset = 0.2f;
        Assert.Equal(1, control.Invalidations);

        control.TextBrush = null;
        control.ResetInvalidations();
        shared.GradientStops[0].Offset = 0.3f;
        Assert.Equal(0, control.Invalidations);
    }

    [Fact]
    public void DisposalDetachesBrushInvalidation()
    {
        var brush = new SolidColorBrush(Color.Red);
        var control = new InvalidationProbeControl { BackgroundBrush = brush };
        using var surface = new SkiaControlSurface(control);
        control.ResetInvalidations();

        control.Dispose();
        brush.PaintColor = Color.Blue;

        Assert.Equal(0, control.Invalidations);
    }

    [Fact]
    public void LongLivedResourceBrushDoesNotKeepForgottenControlAlive()
    {
        object key = NewKey();
        var brush = new SolidColorBrush(Color.Red);
        Application.Resources[key] = brush;

        try
        {
            WeakReference reference = CreateCollectibleBrushBoundControl(key);

            CollectGarbage();
            brush.PaintColor = Color.Blue;

            Assert.False(reference.IsAlive);
        }
        finally
        {
            Application.Resources.Remove(key);
        }
    }

    private static LinearGradientBrush Gradient(Color start, Color end)
    {
        var brush = new LinearGradientBrush();
        brush.GradientStops.AddRange([new GradientStop(start, 0f), new GradientStop(end, 1f)]);
        return brush;
    }

    private static object NewKey() => $"BrushResourceTests.{Guid.NewGuid():N}";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleBrushBoundControl(object key)
    {
        var control = new Control();
        control.SetResourceReference(nameof(Control.BackgroundBrush), key);
        return new WeakReference(control);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class InvalidationProbeControl : Control
    {
        public int Invalidations { get; private set; }

        public void ResetInvalidations() => Invalidations = 0;

        protected override void OnInvalidated(EventArgs<Rectangle> e)
        {
            Invalidations++;
            base.OnInvalidated(e);
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
