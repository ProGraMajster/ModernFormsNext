using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed partial class WindowsUiaProviderTests
{
    [Fact]
    public void ScrollComVtableHasExactNativeOrderAndBooleanWidth()
    {
        var node = ScrollRoot();
        using var provider = Create(node);
        IntPtr unknown = WindowsUiaNativeMethods.GetUnknownPointer(provider), scroll = IntPtr.Zero;
        try
        {
            Guid iid = typeof(IScrollProviderAbi).GUID;
            Assert.Equal(0, Marshal.QueryInterface(unknown, in iid, out scroll));
            IntPtr vtable = Marshal.ReadIntPtr(scroll);
            T Method<T>(int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(vtable, slot * IntPtr.Size));
            Assert.Equal(0, Method<ScrollPercentCall>(4)(scroll, 80, -1));
            Assert.Equal(80, Assert.IsType<PlatformAccessibleScrollRequest>(node.LastParameter).Horizontal);
            Assert.Equal(0, Method<ScrollAmountCall>(3)(scroll, 4, 2));
            Assert.Equal(2, Assert.IsType<PlatformAccessibleScrollRequest>(node.LastParameter).HorizontalAmount);
            Assert.Equal(0, Method<ScrollDoubleGet>(5)(scroll, out double h)); Assert.Equal(25, h);
            Assert.Equal(0, Method<ScrollDoubleGet>(6)(scroll, out double v)); Assert.Equal(-1, v);
            Assert.Equal(0, Method<ScrollDoubleGet>(7)(scroll, out double hw)); Assert.Equal(200d / 300 * 100, hw, 8);
            Assert.Equal(0, Method<ScrollDoubleGet>(8)(scroll, out double vw)); Assert.Equal(100, vw);
            Assert.Equal(0, Method<ScrollBoolGet>(9)(scroll, out int hs)); Assert.Equal(1, hs);
            Assert.Equal(0, Method<ScrollBoolGet>(10)(scroll, out int vs)); Assert.Equal(0, vs);
        }
        finally { if (scroll != IntPtr.Zero) Marshal.Release(scroll); Marshal.Release(unknown); }
    }

    [Fact]
    public async Task RealHwndScrollPatternsRouteCanonicalActionsAndObserveClientFocusPolicy()
    {
        if (!OperatingSystem.IsWindows()) return;
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string rootPath = FindRepositoryRoot();
        string Tool(string name) => System.IO.Path.Combine(rootPath, name, "bin", configuration, "net10.0-windows", name + ".dll");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using Process host = StartScrollProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost"), "--scroll");
        IntPtr hwnd = IntPtr.Zero;
        try
        {
            string? line = await ReadLineStartingWithAsync(host, "HWND:", timeout.Token);
            Assert.NotNull(line);
            Assert.True(long.TryParse(line.AsSpan(5), out long raw));
            hwnd = new(raw);
            using Process client = StartScrollProcess(Tool("ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient"), "--scroll", raw.ToString());
            var outputTask = client.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = client.StandardError.ReadToEndAsync(timeout.Token);
            try { await client.WaitForExitAsync(timeout.Token); }
            finally { if (!client.HasExited) client.Kill(entireProcessTree: true); }
            string output = await outputTask, error = await errorTask;
            Assert.True(client.ExitCode == 0, $"Native Scroll client failed: {error}\n{output}");
            using var result = JsonDocument.Parse(output);
            foreach (string property in new[] { "TargetInitiallyOffscreen", "Horizontal", "Vertical", "SmallMovedBack",
                "TargetRevealed", "FocusBefore", "CanonicalFocusPreserved",
                "InvalidRejected", "DisabledGeometryRetained", "DisabledRejected" })
                Assert.True(result.RootElement.GetProperty(property).GetBoolean(), $"{property}: {output}");
            Assert.Equal(2, result.RootElement.GetProperty("CanonicalScrollActions").GetInt32());
            Assert.InRange(result.RootElement.GetProperty("HorizontalView").GetDouble(), 1, 99);
            Assert.InRange(result.RootElement.GetProperty("VerticalView").GetDouble(), 1, 99);
            Assert.InRange(result.RootElement.GetProperty("HorizontalAfter").GetDouble(), 49, 51);
            Assert.Equal(100, result.RootElement.GetProperty("VerticalAfter").GetDouble());
        }
        finally
        {
            if (hwnd != IntPtr.Zero) _ = PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await host.WaitForExitAsync(cleanup.Token); }
            catch (OperationCanceledException) when (!host.HasExited) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
        }
        Assert.Equal(0, host.ExitCode);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ScrollAmountCall(IntPtr self, int horizontal, int vertical);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ScrollPercentCall(IntPtr self, double horizontal, double vertical);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ScrollDoubleGet(IntPtr self, out double value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ScrollBoolGet(IntPtr self, out int value);

    private static Process StartScrollProcess(string assembly, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start native Scroll test process.");
    }
}
