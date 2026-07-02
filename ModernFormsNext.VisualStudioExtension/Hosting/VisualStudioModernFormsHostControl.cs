using System.Reflection;
using System.Runtime.InteropServices;
using ModernFormsNext.Designer;
using ModernFormsNext.WindowKit.Platform;
using WinForms = System.Windows.Forms;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

internal sealed class VisualStudioModernFormsHostControl : WinForms.UserControl
{
    private const int GwlStyle = -16;
    private const int SwShowNoActivate = 4;

    private const long WsChild = 0x40000000L;
    private const long WsVisible = 0x10000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsCaption = 0x00C00000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private static readonly FieldInfo WindowField = typeof(WindowBase).GetField(
        "window",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ModernFormsNext window field could not be located.");

    private Form? hostForm;
    private IntPtr hostedWindowHandle;

    public VisualStudioModernFormsHostControl()
    {
        Dock = WinForms.DockStyle.Fill;
        BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
    }

    public ModernFormsDesignerShell? Shell { get; private set; }

    public void AttachShell(ModernFormsDesignerShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        DetachShell();

        Shell = shell;

        if (IsHandleCreated)
            CreateHostedWindow();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (Shell is not null && hostForm is null)
            CreateHostedWindow();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResizeHostedWindow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DetachShell();

        base.Dispose(disposing);
    }

    private void CreateHostedWindow()
    {
        if (Shell is null)
            return;

        var clientSize = ClientSize;

        hostForm = new Form
        {
            Name = "ModernFormsNextVisualStudioDesignerHost",
            Text = "ModernFormsNext Designer",
            Resizeable = false,
            StartPosition = FormStartPosition.Manual,
            Size = new System.Drawing.Size(Math.Max(1, clientSize.Width), Math.Max(1, clientSize.Height))
        };

        // The Visual Studio pane provides the chrome. The ModernFormsNext form is only a
        // lightweight HWND surface used to render and route input for the shared designer shell.
        hostForm.TitleBar.Visible = false;
        hostForm.Style.Border.Width = 0;

        Shell.Dock = DockStyle.Fill;
        hostForm.Controls.Add(Shell);
        hostForm.Show();

        hostedWindowHandle = GetWindowHandle(hostForm);
        ConfigureHostedWindowStyle(hostedWindowHandle, Handle);
        ResizeHostedWindow();
    }

    private void DetachShell()
    {
        if (hostForm is not null)
        {
            hostForm.Close();
            hostForm = null;
        }

        hostedWindowHandle = IntPtr.Zero;
        Shell = null;
    }

    private void ResizeHostedWindow()
    {
        if (hostForm is null || hostedWindowHandle == IntPtr.Zero || !IsHandleCreated)
            return;

        var width = Math.Max(1, ClientSize.Width);
        var height = Math.Max(1, ClientSize.Height);
        hostForm.Size = new System.Drawing.Size(width, height);

        SetWindowPos(
            hostedWindowHandle,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    private static IntPtr GetWindowHandle(Form form)
    {
        var window = WindowField.GetValue(form)
            ?? throw new InvalidOperationException("The ModernFormsNext host window has not been initialized.");

        var handle = ((IWindowBaseImpl)window).Handle.Handle;

        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("The ModernFormsNext host window does not expose a native HWND.");

        return handle;
    }

    private static void ConfigureHostedWindowStyle(IntPtr childHandle, IntPtr parentHandle)
    {
        var style = GetWindowLongPtr(childHandle, GwlStyle).ToInt64();
        style &= ~(WsPopup | WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox);
        style |= WsChild | WsVisible;

        SetWindowLongPtr(childHandle, GwlStyle, new IntPtr(style));
        SetParent(childHandle, parentHandle);
        ShowWindow(childHandle, SwShowNoActivate);
        SetWindowPos(
            childHandle,
            IntPtr.Zero,
            0,
            0,
            1,
            1,
            SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
