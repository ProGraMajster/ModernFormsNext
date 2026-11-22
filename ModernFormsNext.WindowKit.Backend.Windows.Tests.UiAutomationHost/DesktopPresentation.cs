using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>Observes presentation on the display containing the test HWND.</summary>
internal sealed unsafe class DesktopPresentation : IDisposable
{
    private const int DxgiNotFound = unchecked((int)0x887A0002);
    private const int DxgiWaitTimeout = unchecked((int)0x887A0027);
    private static readonly Guid Factory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid Output1 = new("00cddea8-939b-4b83-a340-a685226666cc");

    private nint duplication;

    internal DesktopPresentation(nint window)
    {
        var monitor = MonitorFromWindow(window, 2); // MONITOR_DEFAULTTONEAREST
        Marshal.ThrowExceptionForHR(CreateDXGIFactory1(Factory1, out var factory));
        try
        {
            duplication = DuplicateMonitor(factory, monitor);
        }
        finally { Marshal.Release(factory); }
    }

    internal void WaitForPresentAfter(long paintFinished, TimeSpan remaining)
    {
        long start = Stopwatch.GetTimestamp();
        while (true)
        {
            var timeout = (remaining - Stopwatch.GetElapsedTime(start)).TotalMilliseconds;
            if (timeout <= 0)
                throw new TimeoutException("No desktop image was presented after repainting the test window.");

            // IDXGIOutputDuplication::AcquireNextFrame can also wake for pointer-only changes.
            FrameInfo frame;
            nint resource;
            var acquire = (delegate* unmanaged[Stdcall]<nint, uint, FrameInfo*, nint*, int>)VTable(duplication)[8];
            int result = acquire(duplication, (uint)timeout, &frame, &resource);
            if (result == DxgiWaitTimeout)
                throw new TimeoutException("No desktop image was presented after repainting the test window.");
            Marshal.ThrowExceptionForHR(result);

            Marshal.Release(resource);
            // IDXGIOutputDuplication::ReleaseFrame
            var release = (delegate* unmanaged[Stdcall]<nint, int>)VTable(duplication)[14];
            Marshal.ThrowExceptionForHR(release(duplication));

            // LastPresentTime uses the same QPC time base as Stopwatch. The pixel is read
            // once after this event, so an incorrect pixel in the presented frame still fails.
            if (frame.LastPresentTime >= paintFinished)
                return;
        }
    }

    private static nint DuplicateMonitor(nint factory, nint monitor)
    {
        // IDXGIFactory1::EnumAdapters1 and IDXGIAdapter::EnumOutputs
        var enumerateAdapters = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)VTable(factory)[12];
        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            nint adapter;
            int result = enumerateAdapters(factory, adapterIndex, &adapter);
            if (result == DxgiNotFound) break;
            Marshal.ThrowExceptionForHR(result);
            try
            {
                var enumerateOutputs = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)VTable(adapter)[7];
                for (uint outputIndex = 0; ; outputIndex++)
                {
                    nint output;
                    result = enumerateOutputs(adapter, outputIndex, &output);
                    if (result == DxgiNotFound) break;
                    Marshal.ThrowExceptionForHR(result);
                    try
                    {
                        // IDXGIOutput::GetDesc
                        OutputDescription description;
                        var describe = (delegate* unmanaged[Stdcall]<nint, OutputDescription*, int>)VTable(output)[7];
                        Marshal.ThrowExceptionForHR(describe(output, &description));
                        if (description.Monitor == monitor)
                            return DuplicateOutput(adapter, output);
                    }
                    finally { Marshal.Release(output); }
                }
            }
            finally { Marshal.Release(adapter); }
        }

        throw new InvalidOperationException("DXGI could not identify the test window's monitor.");
    }

    private static nint DuplicateOutput(nint adapter, nint output)
    {
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(output, Output1, out var output1));
        try
        {
            // Desktop duplication requires a device created on the adapter driving the output.
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(adapter, 0, 0, 0, 0, 0, 7, out var device, 0, 0));
            try
            {
                // IDXGIOutput1::DuplicateOutput
                nint duplication;
                var duplicate = (delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)VTable(output1)[22];
                Marshal.ThrowExceptionForHR(duplicate(output1, device, &duplication));
                return duplication;
            }
            finally { Marshal.Release(device); }
        }
        finally { Marshal.Release(output1); }
    }

    private static void** VTable(nint instance) => *(void***)instance;

    public void Dispose()
    {
        if (duplication == 0) return;
        Marshal.Release(duplication);
        duplication = 0;
    }

    // DXGI_OUTPUT_DESC and DXGI_OUTDUPL_FRAME_INFO retain the full native layout.
    [StructLayout(LayoutKind.Sequential)]
    private struct OutputDescription
    {
        internal fixed char DeviceName[32];
        internal int Left, Top, Right, Bottom, AttachedToDesktop, Rotation;
        internal nint Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameInfo
    {
        internal long LastPresentTime, LastMouseUpdateTime;
        internal uint AccumulatedFrames, RectsCoalesced, ProtectedContentMaskedOut;
        internal int PointerX, PointerY, PointerVisible;
        internal uint TotalMetadataBufferSize, PointerShapeBufferSize;
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(in Guid iid, out nint factory);
    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags,
        nint featureLevels, uint featureLevelsCount, uint sdkVersion, out nint device,
        nint featureLevel, nint immediateContext);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);
}
