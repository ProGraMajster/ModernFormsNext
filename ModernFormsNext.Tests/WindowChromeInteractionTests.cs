using System.Reflection;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Controls;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class WindowChromeInteractionTests
{
    [Fact]
    public void TitleBarMoveDragIsAllowedForNormalAndStandaloneButSuppressedForEmbeddedChild()
    {
        var implementation = DispatchProxy.Create<IWindowImpl, RecordingWindowProxy>();
        var recording = (RecordingWindowProxy)(object)implementation;
        using var factory = TestWindowFactoryScope.Push(() => implementation);
        using var form = new Form();
        var titleBar = new TestFormTitleBar();
        form.Controls.Add(titleBar);

        titleBar.RaiseMouseDown();

        Assert.Equal(1, recording.MoveDragCount);

        form.ChromeInteractionMode = Form.WindowChromeInteractionMode.EmbeddedChild;
        titleBar.RaiseMouseDown();

        Assert.Equal(1, recording.MoveDragCount);

        // Standalone Designer deliberately uses the same explicit top-level chrome contract as
        // a normal Form; integrated hosting is the only mode that suppresses the operation.
        form.ChromeInteractionMode = Form.WindowChromeInteractionMode.TopLevel;
        titleBar.RaiseMouseDown();

        Assert.Equal(2, recording.MoveDragCount);
    }

    [Fact]
    public void TopLevelFormRoutesEveryResizeEdgeAndCorner()
    {
        var implementation = DispatchProxy.Create<IWindowImpl, RecordingWindowProxy>();
        var recording = (RecordingWindowProxy)(object)implementation;
        using var factory = TestWindowFactoryScope.Push(() => implementation);
        using var form = new Form();
        var width = form.ScaledSize.Width;
        var height = form.ScaledSize.Height;
        var centerX = width / 2;
        var centerY = height / 2;
        var cases = new[]
        {
            (0, 0, WindowEdge.NorthWest),
            (centerX, 0, WindowEdge.North),
            (width - 1, 0, WindowEdge.NorthEast),
            (width - 1, centerY, WindowEdge.East),
            (width - 1, height - 1, WindowEdge.SouthEast),
            (centerX, height - 1, WindowEdge.South),
            (0, height - 1, WindowEdge.SouthWest),
            (0, centerY, WindowEdge.West)
        };

        foreach (var (x, y, expectedEdge) in cases)
        {
            Assert.True(form.HandleMouseDown(x, y));
            Assert.Equal(expectedEdge, recording.ResizeEdges[^1]);
        }

        Assert.Equal(cases.Length, recording.ResizeEdges.Count);
    }

    [Fact]
    public void EmbeddedChildSuppressesWindowLevelResizeRouting()
    {
        var implementation = DispatchProxy.Create<IWindowImpl, RecordingWindowProxy>();
        var recording = (RecordingWindowProxy)(object)implementation;
        using var factory = TestWindowFactoryScope.Push(() => implementation);
        using var form = new Form
        {
            ChromeInteractionMode = Form.WindowChromeInteractionMode.EmbeddedChild
        };

        Assert.False(form.HandleMouseDown(0, 0));
        Assert.False(form.HandleMouseMove(0, 0));
        Assert.Empty(recording.ResizeEdges);
    }

    private sealed class TestFormTitleBar : FormTitleBar
    {
        public void RaiseMouseDown()
            => OnMouseDown(new MouseEventArgs(
                MouseButtons.Left,
                1,
                10,
                10,
                System.Drawing.Point.Empty));
    }

    private class RecordingWindowProxy : DispatchProxy
    {
        private Size clientSize = new(800, 600);

        public int MoveDragCount { get; private set; }

        public List<WindowEdge> ResizeEdges { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_ClientSize":
                    return clientSize;
                case "get_RenderScaling":
                case "get_DesktopScaling":
                    return 1d;
                case "get_Position":
                    return PixelPoint.Origin;
                case "get_Handle":
                    return new PlatformHandle(IntPtr.Zero, "TEST");
                case "Resize":
                    clientSize = (Size)args![0]!;
                    return null;
                case "BeginMoveDrag":
                    MoveDragCount++;
                    return null;
                case "BeginResizeDrag":
                    ResizeEdges.Add((WindowEdge)args![0]!);
                    return null;
            }

            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
