using ModernFormsNext.Designer;
using WinForms = System.Windows.Forms;

namespace ModernFormsNext.VisualStudioExtension.Hosting;

internal sealed class VisualStudioModernFormsHostControl : WinForms.UserControl
{
    private readonly VisualStudioDesignerHostLifecycle lifecycle = new(new WindowsNativeWindowOperations());
    private Form? hostForm;

    public VisualStudioModernFormsHostControl()
    {
        Dock = WinForms.DockStyle.Fill;
        BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
    }

    public ModernFormsDesignerShell? Shell { get; private set; }

    internal string? LastHostDiagnostic => lifecycle.LastDiagnostic;

    public void AttachShell(ModernFormsDesignerShell shell)
    {
        ArgumentNullException.ThrowIfNull(shell);

        DetachShell();
        Shell = shell;

        if (!IsHandleCreated)
            return;

        try
        {
            CreateHostedWindow();
        }
        catch
        {
            Shell = null;
            throw;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (Shell is not null && hostForm is null)
            CreateHostedWindow();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        // WinForms may recreate the pane HWND during docking or DPI transitions. The hosted
        // ModernFormsNext window must be detached from the obsolete parent and recreated when
        // the new pane handle becomes available, while retaining the shared Designer shell.
        DestroyHostedWindow();
        base.OnHandleDestroyed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        TryResizeHostedWindow();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);

        try
        {
            lifecycle.UpdateDpi(DeviceDpi, ClientSize.Width, ClientSize.Height);
        }
        catch (InvalidOperationException)
        {
            // The lifecycle retains the diagnostic. A transient native resize failure must not
            // unwind Visual Studio's synchronous DPI notification.
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        TryFocusHostedWindow();
    }

    protected override void OnMouseDown(WinForms.MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        TryFocusHostedWindow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachShell();
            lifecycle.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CreateHostedWindow()
    {
        if (Shell is null)
            return;

        var shell = Shell;
        var clientSize = ClientSize;
        var replacementForm = new Form
        {
            Name = "ModernFormsNextVisualStudioDesignerHost",
            Text = "ModernFormsNext Designer",
            Resizeable = false,
            StartPosition = FormStartPosition.Manual,
            Size = new System.Drawing.Size(Math.Max(1, clientSize.Width), Math.Max(1, clientSize.Height))
        };

        // Visual Studio owns the chrome. The ModernFormsNext form supplies only the native
        // rendering/input surface and keeps the shared Designer shell independent of VSSDK.
        replacementForm.TitleBar.Visible = false;
        replacementForm.Style.Border.Width = 0;
        shell.Dock = DockStyle.Fill;
        replacementForm.Controls.Add(shell);

        try
        {
            replacementForm.Show();

            var platformHandle = replacementForm.PlatformHandle;
            if (!string.Equals(platformHandle.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException(
                    $"Visual Studio hosting requires an HWND, but the active backend reported '{platformHandle.HandleDescriptor ?? "<none>"}'.");
            }

            lifecycle.Attach(platformHandle.Handle, Handle);
            lifecycle.UpdateDpi(DeviceDpi, clientSize.Width, clientSize.Height);
            hostForm = replacementForm;
        }
        catch
        {
            if (lifecycle.State != VisualStudioDesignerHostState.Disposed)
                lifecycle.Detach();

            replacementForm.Controls.Remove(shell);
            replacementForm.Close();
            replacementForm.Dispose();
            throw;
        }
    }

    private void DetachShell()
    {
        DestroyHostedWindow();
        Shell = null;
    }

    private void DestroyHostedWindow()
    {
        if (lifecycle.State != VisualStudioDesignerHostState.Disposed)
            lifecycle.Detach();

        if (hostForm is not null)
        {
            if (Shell is not null)
                hostForm.Controls.Remove(Shell);

            hostForm.Close();
            hostForm.Dispose();
            hostForm = null;
        }
    }

    private void TryResizeHostedWindow()
    {
        try
        {
            lifecycle.Resize(ClientSize.Width, ClientSize.Height);
        }
        catch (InvalidOperationException)
        {
            // Keep Visual Studio responsive and retain LastHostDiagnostic for troubleshooting.
        }
    }

    private void TryFocusHostedWindow()
    {
        try
        {
            lifecycle.Focus();
        }
        catch (InvalidOperationException)
        {
            // Windows can deny focus while Visual Studio is changing frames. The next focus
            // notification retries, while LastHostDiagnostic preserves the original reason.
        }
    }
}
