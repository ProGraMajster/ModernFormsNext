using System.Reflection;
using ModernFormsNext.WindowKit.Platform;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class WindowPlatformHandleTests
{
    [Fact]
    public void PlatformHandleReturnsTheTypedBackendHandleWithoutTakingOwnership()
    {
        var expected = new PlatformHandle(new IntPtr(1234), "TEST-WINDOW");
        var implementation = DispatchProxy.Create<IWindowBaseImpl, WindowImplProxy>();
        ((WindowImplProxy)(object)implementation).PlatformHandle = expected;

        using var window = new TestWindow(implementation);

        Assert.Same(expected, window.PlatformHandle);
        Assert.Equal(new IntPtr(1234), window.PlatformHandle.Handle);
        Assert.Equal("TEST-WINDOW", window.PlatformHandle.HandleDescriptor);
    }

    private sealed class TestWindow : WindowBase
    {
        public TestWindow(IWindowBaseImpl implementation)
            : base(implementation)
        {
        }
    }

    private class WindowImplProxy : DispatchProxy
    {
        public IPlatformHandle? PlatformHandle { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_Handle")
                return PlatformHandle;
            if (targetMethod is null || targetMethod.ReturnType == typeof(void))
                return null;

            return targetMethod.ReturnType.IsValueType
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
