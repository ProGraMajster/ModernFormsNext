using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using System.Runtime.InteropServices;
using static ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32.Input;

// The small native boundary permits deterministic buffer/lifetime tests without changing
// a user's keyboard profile. All document/focus behavior remains in the offered client.
internal interface IImm32NativeApi
{
    IntPtr GetContext(IntPtr hwnd);
    void ReleaseContext(IntPtr hwnd, IntPtr context);
    IntPtr AssociateContext(IntPtr hwnd, IntPtr context);
    int ReadComposition(IntPtr context, GCS index, Span<byte> buffer);
    void CancelComposition(IntPtr context);
    IntPtr FocusedWindow { get; }
    IntPtr CaretWindow { get; }
    void PositionComposition(IntPtr context, COMPOSITIONFORM form);
    void PositionCandidates(IntPtr context, CANDIDATEFORM form);
    bool CreateCaret(IntPtr hwnd, int width, int height);
    void PositionCaret(int x, int y);
    void DestroyCaret();
}

internal sealed class Imm32NativeApi : IImm32NativeApi
{
    internal static readonly Imm32NativeApi Instance = new();
    public IntPtr GetContext(IntPtr hwnd) => ImmGetContext(hwnd);
    public void ReleaseContext(IntPtr hwnd, IntPtr context) => ImmReleaseContext(hwnd, context);
    public IntPtr AssociateContext(IntPtr hwnd, IntPtr context) => ImmAssociateContext(hwnd, context);
    public unsafe int ReadComposition(IntPtr context, GCS index, Span<byte> buffer)
    {
        fixed (byte* bytes = buffer)
            return ImmGetCompositionString(context, index, (IntPtr)bytes, (uint)buffer.Length);
    }
    public void CancelComposition(IntPtr context) => ImmNotifyIME(context, NI_COMPOSITIONSTR, CPS_CANCEL, 0);
    public IntPtr FocusedWindow => GetFocus();
    public IntPtr CaretWindow
    {
        get
        {
            var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
            return GetGUIThreadInfo(GetCurrentThreadId(), ref info) ? info.Caret : IntPtr.Zero;
        }
    }
    public void PositionComposition(IntPtr context, COMPOSITIONFORM form) => ImmSetCompositionWindow(context, ref form);
    public void PositionCandidates(IntPtr context, CANDIDATEFORM form) => ImmSetCandidateWindow(context, ref form);
    public bool CreateCaret(IntPtr hwnd, int width, int height) => UnmanagedMethods.CreateCaret(hwnd, IntPtr.Zero, width, height);
    public void PositionCaret(int x, int y) => SetCaretPos(x, y);
    public void DestroyCaret() => UnmanagedMethods.DestroyCaret();

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal uint Size, Flags;
        internal IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        internal RECT CaretRectangle;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetCurrentThreadId();
}
