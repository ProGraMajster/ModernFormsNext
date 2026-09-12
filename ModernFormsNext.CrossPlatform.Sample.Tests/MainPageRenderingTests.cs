using ModernFormsNext.Testing;
using ModernFormsNext.WindowKit.Backend;
using ModernFormsNext.WindowKit.Platform.Permissions;
using ModernFormsNext.WindowKit.Threading;
using SkiaSharp;
using Xunit.Abstractions;

namespace ModernFormsNext.CrossPlatform.Sample.Tests;

[Collection("Sample accessibility preferences")]
public sealed class MainPageRenderingTests(ITestOutputHelper output)
{
    [Fact]
    public void NormalPageCompletesFirstAndSecondRenderAtAndroidViewport()
    {
        using var host = ModernFormsTestHost.Create();
        var app = new App(new Platform());
        using var root = app.Root;
        string stage = "attach";
        int layouts = 0, invalidations = 0;
        var controls = Descendants(root).Prepend(root).ToArray();
        EventHandler<LayoutEventArgs> layout = (sender, _) =>
        {
            if (++layouts > 10000)
                throw new InvalidOperationException($"Layout did not settle during {stage}; control={sender?.GetType().Name}.");
        };
        EventHandler<EventArgs<System.Drawing.Rectangle>> invalidated = (sender, _) =>
        {
            if (++invalidations > 25000)
                throw new InvalidOperationException($"Invalidation did not settle during {stage}; control={sender?.GetType().Name}.");
        };
        foreach (var control in controls)
        {
            control.Layout += layout;
            control.Invalidated += invalidated;
        }
        try
        {
            using var surface = new SkiaControlSurface(root);
            using var bitmap = new SKBitmap(411, 840);
            using var canvas = new SKCanvas(bitmap);
            stage = "resize";
            output.WriteLine(stage);
            surface.Resize(411, 840);
            host.ProcessPendingWork();
            stage = "first render";
            output.WriteLine(stage);
            surface.Render(canvas);
            canvas.Flush();
            Assert.Equal(1, app.State.RenderCount);
            stage = "status refresh";
            output.WriteLine(stage);
            app.RefreshPlatformStatus();
            host.ProcessPendingWork();
            stage = "second render";
            output.WriteLine(stage);
            surface.Render(canvas);
            canvas.Flush();
            host.ProcessPendingWork();
            Assert.Equal(2, app.State.RenderCount);
            Assert.Equal(411, app.State.SurfaceWidth);
            Assert.Equal(840, app.State.SurfaceHeight);
            Assert.Contains(controls.OfType<TextBox>(), text => text.Top > root.Height);
            Assert.False(controls.OfType<CheckBox>().Single(control => control.Name == "FollowAccessibilityPreferences").Checked);
            output.WriteLine($"Completed: layouts={layouts}, invalidations={invalidations}.");
        }
        finally
        {
            foreach (var control in controls)
            {
                control.Layout -= layout;
                control.Invalidated -= invalidated;
            }
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private sealed class Platform : ISamplePlatformServices
    {
        public string PlatformName => "Headless Android viewport";
        public string OperatingSystem => "Test OS";
        public string BackendName => "Test backend";
        public string HostState => "Attached";
        public string AnimationRuntimeStatus => "Test";
        public IPlatformDispatcher Dispatcher => PlatformServiceRegistry.GetRequiredService<IPlatformDispatcher>();
        public bool SupportsPermissionAction => false;
        public Task<PlatformPermissionStatus> CheckSamplePermissionAsync() => Task.FromResult(PlatformPermissionStatus.NotSupported);
        public Task<PlatformPermissionStatus> RequestSamplePermissionAsync() => Task.FromResult(PlatformPermissionStatus.NotSupported);
        public Task<bool> OpenApplicationSettingsAsync() => Task.FromResult(false);
    }
}
