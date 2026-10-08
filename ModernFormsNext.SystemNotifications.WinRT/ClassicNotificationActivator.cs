using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.SystemNotifications.WinRT;

// The installer owns LocalServer32, the AUMID shortcut and ToastActivatorCLSID. This class
// only exposes the live process's COM class factory, and revokes it on the same apartment.
internal sealed class ClassicNotificationActivator : IAsyncDisposable
{
    private uint cookie;
    private readonly Factory factory;

    internal ClassicNotificationActivator(Guid clsid, string? appId, Action<string, IReadOnlyDictionary<string, string>> callback)
    {
        factory = new Factory(new Callback(appId, callback));
        Marshal.ThrowExceptionForHR(CoRegisterClassObject(ref clsid, factory, 4, 1, out cookie));
    }

    public ValueTask DisposeAsync()
    {
        if (Dispatcher.UIThread.CheckAccess()) { Revoke(); return ValueTask.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            try { Revoke(); completion.TrySetResult(); }
            catch (Exception e) { completion.TrySetException(e); }
        });
        return new(completion.Task);
    }
    private void Revoke()
    {
        factory.Stop();
        if (cookie != 0) { uint old = cookie; cookie = 0; Marshal.ThrowExceptionForHR(CoRevokeClassObject(old)); }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Factory(Callback callback) : IClassicNotificationClassFactory
    {
        private int stopped;
        public void Stop() { Interlocked.Exchange(ref stopped, 1); callback.Stop(); }
        public int CreateInstance(nint outer, ref Guid iid, out nint result)
        {
            result = 0;
            if (outer != 0) return unchecked((int)0x80040110); // CLASS_E_NOAGGREGATION
            if (Volatile.Read(ref stopped) != 0) return unchecked((int)0x80040111);
            nint unknown = 0;
            try
            {
                unknown = Marshal.GetIUnknownForObject(callback);
                return Marshal.QueryInterface(unknown, in iid, out result);
            }
            catch (Exception e) { return e.HResult; }
            finally { if (unknown != 0) Marshal.Release(unknown); }
        }
        public int LockServer(bool locked) => 0; // Application.Run owns process lifetime; COM holds its own interface references.
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Callback(string? expectedAppId, Action<string, IReadOnlyDictionary<string, string>> callback) : IClassicNotificationActivationCallback
    {
        private int stopped;
        public void Stop() => Interlocked.Exchange(ref stopped, 1);
        public int Activate(string appUserModelId, string invokedArgs, nint data, uint count)
        {
            try
            {
                if (Volatile.Read(ref stopped) != 0) return 0;
                if (expectedAppId is not null && appUserModelId != expectedAppId || count > 5 || count > 0 && data == 0)
                    return unchecked((int)0x80070057);
                var input = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    nint item = data + i * (2 * nint.Size);
                    input.Add(ReadString(Marshal.ReadIntPtr(item), 64), ReadString(Marshal.ReadIntPtr(item + nint.Size), 4096));
                }
                callback(invokedArgs, input);
                return 0;
            }
            catch (Exception e) { return e.HResult; } // Never unwind managed exceptions through the native COM ABI.
        }
        private static string ReadString(nint value, int maximum)
        {
            if (value == 0) return string.Empty;
            for (int i = 0; i <= maximum; i++)
                if (Marshal.ReadInt16(value, i * 2) == 0) return Marshal.PtrToStringUni(value, i)!;
            throw new ArgumentException("Activation input exceeds the documented bound.");
        }
    }

    [DllImport("ole32.dll")] private static extern int CoRegisterClassObject(ref Guid clsid,
        [MarshalAs(UnmanagedType.Interface)] IClassicNotificationClassFactory factory, uint context, uint flags, out uint cookie);
    [DllImport("ole32.dll")] private static extern int CoRevokeClassObject(uint cookie);
}

/// <summary>Native COM class-factory ABI. Infrastructure only; applications use the notification service.</summary>
[ComVisible(true), Guid("00000001-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public interface IClassicNotificationClassFactory
{
    /// <summary>Creates an interface owned by the COM caller; returns an HRESULT.</summary>
    [PreserveSig] int CreateInstance(nint outer, ref Guid iid, out nint result);
    /// <summary>Receives the native server-lock request; framework application lifetime remains authoritative.</summary>
    [PreserveSig] int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
}

/// <summary>Microsoft's native INotificationActivationCallback ABI for installed desktop toast activation.</summary>
[ComVisible(true), Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public interface IClassicNotificationActivationCallback
{
    /// <summary>Copies OS-owned arguments and NOTIFICATION_USER_INPUT_DATA before returning an HRESULT.</summary>
    [PreserveSig] int Activate([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string invokedArgs, nint data, uint count);
}
