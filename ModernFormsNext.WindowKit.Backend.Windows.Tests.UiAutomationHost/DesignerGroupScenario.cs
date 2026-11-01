using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.CodeGeneration.CSharp;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Services;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.WindowKit.Threading;

// Extends the existing native certification host. Synchronous messages target only this HWND;
// thread-local keyboard state is restored. This is backend/routed-input evidence, not SendInput
// or a claim of human Visual Studio acceptance. No project code is loaded by the fixture.
internal static class DesignerGroupScenario
{
    internal static int Run()
    {
        try
        {
            var document = new DesignDocument { Namespace = "DesignerCertification", ClassName = "GroupForm", FormName = "groupForm", Size = new(480, 360) };
            var first = Node("firstButton", new(10, 20, 100, 30), "First");
            var second = Node("secondButton", new(80, 60, 120, 40), "Second");
            document.Controls.Add(first); document.Controls.Add(second);
            using var form = new VerificationForm { Text = "Designer group certification", ClientSize = new(1000, 700) };
            var shell = form.Controls.Add(new ModernFormsDesignerShell(new ModernFormsDesignerOptions
            {
                InitialControlRenderMode = DesignerControlRenderMode.Placeholder, AutoSaveEnabled = false,
                ShowSolutionExplorer = false, ShowOutputPanel = false, Language = DesignerLanguage.English
            }) { Dock = DockStyle.Fill });
            form.Shell = shell;
            shell.LoadDocument(document);
            var session = shell.Session;
            var surface = Descendants(shell).OfType<DesignerSurface>().Single();
            var grid = Descendants(shell).OfType<DesignerPropertyGrid>().Single();
            var toolbar = Descendants(shell).OfType<DesignerToolbar>().Single();
            // Observe the actual rendered grid, not a second descriptor/editor state.
            var properties = (DesignerPropertyGridState)typeof(DesignerPropertyGrid)
                .GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "An actual HWND is required.");
                    Require(SetWindowPos(hwnd, new nint(-1), 40, 40, 0, 0, 0x0011), "Cannot expose the owned certification window.");
                    form.SetTextInputActive(true);
                    Click(40, 35);
                    Click(140, 80, Keys.Control);
                    Require(session.SelectedNodes.SequenceEqual(new[] { first, second }) && ReferenceEquals(session.SelectedNode, second), "Native Ctrl-click did not extend selection.");
                    Click(40, 35, Keys.Control);
                    Require(session.SelectedNodes.SequenceEqual(new[] { second }), "Native Ctrl-click did not toggle membership.");
                    Click(40, 35, Keys.Shift);
                    Require(session.SelectedNodes.SequenceEqual(new[] { second, first }) && ReferenceEquals(session.SelectedNode, first), "Native Shift-click did not extend with a primary.");
                    Require(!session.Transactions.CanUndo, "Selection created model history.");
                    Console.WriteLine("DESIGNER_GROUPS:NATIVE_MODIFIERS:PASS");

                    var dragStart = MappedPoint(40, 35);
                    var dragEnd = MappedPoint(60, 50);
                    int deltaX = dragEnd.X - dragStart.X, deltaY = dragEnd.Y - dragStart.Y;
                    Pointer(40, 35, 0x0201, 1);
                    Pointer(60, 50, 0x0200, 1);
                    Pointer(60, 50, 0x0202);
                    Require(first.Bounds == new DesignBounds(10 + deltaX, 20 + deltaY, 100, 30)
                        && second.Bounds == new DesignBounds(80 + deltaX, 60 + deltaY, 120, 40), "Native drag did not retain one common delta.");
                    Require(session.CurrentHistory.UndoCount == 1 && session.Transactions.Undo(), "Native group drag was not one undo unit.");
                    Require(first.Bounds.X == 10 && second.Bounds.X == 80, "Native group drag undo was incomplete.");

                    Pointer(1, 1, 0x0201, 1);
                    Pointer(250, 150, 0x0200, 1);
                    SendKey(0x1B);
                    Pointer(250, 150, 0x0202);
                    Require(surface.MouseController.MarqueeBounds is null && !surface.Capture && session.SelectedNodes.Count == 2, "Escape did not cancel native marquee capture.");
                    Pointer(1, 1, 0x0201, 1);
                    Pointer(250, 150, 0x0200, 1);
                    Pointer(250, 150, 0x0202);
                    Require(session.SelectedNodes.SequenceEqual(new[] { first, second }) && ReferenceEquals(session.SelectedNode, second), "Native marquee did not publish the complete outline selection.");
                    PaintAndCheckBorder();
                    Console.WriteLine("DESIGNER_GROUPS:NATIVE_DRAG_MARQUEE_PIXELS:PASS");

                    var arrange = toolbar.Controls.OfType<Button>().Single(button => button.Text == "Arrange");
                    var arrangeCenter = new Point(arrange.ScaledWidth / 2, arrange.ScaledHeight / 2);
                    ControlPointer(arrange, arrangeCenter, 0x0201, 1);
                    ControlPointer(arrange, arrangeCenter, 0x0202);
                    var menu = (ContextMenu)typeof(DesignerToolbar).GetField("arrangeMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(toolbar)!;
                    Require(menu.Visible, "The Arrange toolbar button did not open its menu.");
                    var expectedOrigin = arrange.PointToScreen(new Point(0, arrange.ScaledHeight));
                    var menuOrigin = menu.PointToScreen(Point.Empty);
                    // Horizontal work-area clamping is legitimate; the anchor must still lie on
                    // the menu's horizontal span. A raw control-local origin cannot satisfy this.
                    Require(Math.Abs(menuOrigin.Y - expectedOrigin.Y) <= 2 && menuOrigin.X <= expectedOrigin.X + 2
                        && menuOrigin.X + menu.ScaledWidth >= expectedOrigin.X, "Arrange menu was not anchored below its owning button.");
                    var align = menu.Items.Single(item => item.Text == "Align left");
                    var itemCenter = new Point(align.Bounds.Left + align.Bounds.Width / 2, align.Bounds.Top + align.Bounds.Height / 2);
                    var itemScreen = menu.PointToScreen(itemCenter);
                    nint popup = WindowFromPoint(new NativePoint { X = itemScreen.X, Y = itemScreen.Y });
                    GetWindowThreadProcessId(popup, out uint popupProcess);
                    Require(popup != 0 && popup != hwnd && popupProcess == (uint)Environment.ProcessId, "Arrange item did not belong to the owned native popup.");
                    ControlPointer(menu, itemCenter, 0x0201, 1, popup);
                    ControlPointer(menu, itemCenter, 0x0202, target: popup);
                    Require(!menu.Visible && first.Bounds.X == second.Bounds.X && first.Bounds.X == 80, "Native Arrange item did not align to the primary.");
                    Require(session.CurrentHistory.UndoCount == 1 && session.Transactions.Undo(), "Arrange did not create one undo unit.");
                    Require(first.Bounds.X == 10 && second.Bounds.X == 80, "Arrange undo did not restore both positions.");
                    Console.WriteLine("DESIGNER_GROUPS:NATIVE_TOOLBAR_MENU_ANCHOR_ACTION:PASS");

                    Require(grid.Visible && grid.CanSelect && form.TextInputDiagnostics.HasNativeMethod,
                        "The actual property panel and native text method must be available.");
                    var text = properties.Properties.Single(property => property.Name == "Text");
                    Require(text.IsMixed, "Expected different authored Text values.");
                    var row = new DesignerPropertyGridRow(text);
                    grid.BeginEdit(row, new Rectangle(140, 150, 170, 24));
                    Require(grid.Controls.OfType<TextBox>().Single().Text.Length == 0, "Mixed editor initialized an authored value.");
                    SendKey(0x0D);
                    Require(first.Properties["Text"].Value is "First" && second.Properties["Text"].Value is "Second" && session.CurrentHistory.UndoCount == 0, "Untouched mixed Enter changed model data.");
                    row = new DesignerPropertyGridRow(properties.Properties.Single(property => property.Name == "Text"));
                    grid.BeginEdit(row, new Rectangle(140, 150, 170, 24));
                    var activeEditor = grid.Controls.OfType<TextBox>().Single();
                    var inputState = form.TextInputDiagnostics;
                    Console.WriteLine($"DESIGNER_GROUPS:EDITOR_STATE ownerActive={form.IsActive} inputActive={inputState.IsActive} client={inputState.HasClient} native={inputState.HasNativeMethod} selected={activeEditor.Selected} selectable={activeEditor.CanSelect} visible={activeEditor.Visible} grid={grid.Bounds} events={string.Join(',', inputState.RecentEvents)}");
                    Require(inputState.HasClient && activeEditor.Selected, "The real editor must own the text session.");
                    Require(form.TextInputClient?.CommitText("Shared") == true, "The native text edit did not complete.");
                    SendKey(0x0D);
                    Require(first.Properties["Text"].Value is "Shared" && second.Properties["Text"].Value is "Shared", "Native editor did not update every selected target.");
                    Require(session.CurrentHistory.UndoCount == 1 && session.Transactions.Undo(), "Native property edit did not form one undo unit.");
                    Require(first.Properties["Text"].Value is "First" && second.Properties["Text"].Value is "Second", "Native property undo was incomplete.");
                    Console.WriteLine("DESIGNER_GROUPS:NATIVE_MIXED_EDITOR_ENTER:PASS");

                    WithModifiers(Keys.Control, () => SendKeyMessages(0x43));
                    Require(session.Clipboard.Content is not null, "Native Ctrl+C did not copy the selection.");
                    session.SelectForm();
                    WithModifiers(Keys.Control, () => SendKeyMessages(0x56));
                    Require(document.Controls.Count == 4 && session.SelectedNodes.Count == 2, "Native Ctrl+V did not paste the full forest.");
                    var encoded = DesignDocumentSerializer.Default.Serialize(document);
                    var restored = DesignDocumentSerializer.Default.Deserialize(encoded);
                    Require(DesignDocumentSerializer.Default.Serialize(restored) == encoded, "Group model did not round trip.");
                    var generated = new CSharpDesignerGenerator().Generate(restored);
                    Require(generated.Succeeded && generated.Code.Contains(session.SelectedNodes[0].Name, StringComparison.Ordinal)
                        && generated.Code.Contains(session.SelectedNodes[1].Name, StringComparison.Ordinal), "Generator omitted a group root.");
                    WithModifiers(Keys.Control, () => SendKeyMessages(0x5A));
                    Require(document.Controls.Count == 2, "Native Ctrl+Z did not restore the source document.");
                    Require(!surface.Capture && !session.Transactions.HasActiveTransaction, "Native gesture resources were retained.");
                    Console.WriteLine($"DESIGNER_GROUPS:NATIVE_ROUND_TRIP:{hwnd}:PASS");

                    void Click(int x, int y, Keys modifiers = Keys.None)
                        => WithModifiers(modifiers, () => { Pointer(x, y, 0x0201, 1); Pointer(x, y, 0x0202); });

                    void Pointer(int x, int y, uint message, int buttons = 0)
                    {
                        var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
                        var point = new Point(surface.LogicalToDeviceUnits(view.ClientX + (int)Math.Round(x * view.Scale)),
                            surface.LogicalToDeviceUnits(view.ClientY + (int)Math.Round(y * view.Scale)));
                        ControlPointer(surface, point, message, buttons);
                    }

                    DesignPoint MappedPoint(int x, int y)
                    {
                        var mapper = new DesignerCoordinateMapper();
                        var view = mapper.GetView(session, surface.Width, surface.Height);
                        var logical = DesignerDpiCoordinateConverter.DeviceToLogical(
                            surface.LogicalToDeviceUnits(view.ClientX + (int)Math.Round(x * view.Scale)),
                            surface.LogicalToDeviceUnits(view.ClientY + (int)Math.Round(y * view.Scale)), surface.Scaling);
                        return mapper.MapToDocument(view, logical.X, logical.Y);
                    }

                    void ControlPointer(Control control, Point localDevicePoint, uint message, int buttons = 0, nint target = 0)
                    {
                        if (target == 0) target = hwnd;
                        var screen = control.PointToScreen(localDevicePoint);
                        var client = new NativePoint { X = screen.X, Y = screen.Y };
                        Require(ScreenToClient(target, ref client), "Cannot map the owned native pointer.");
                        SendMessage(target, message, buttons, Pack(client));
                    }

                    void SendKey(int key) => WithModifiers(Keys.None, () => SendKeyMessages(key));

                    void SendKeyMessages(int key)
                    {
                        SendMessage(hwnd, 0x0100, key, 1);
                        SendMessage(hwnd, 0x0101, key, unchecked((nint)0xC0000001));
                    }

                    void PaintAndCheckBorder()
                    {
                        Require(RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100), "The owned Designer did not paint.");
                        var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
                        var bounds = new DesignerCoordinateMapper().ToSurfaceBounds(first.Bounds, view);
                        var screen = surface.PointToScreen(new Point(surface.LogicalToDeviceUnits(bounds.Left), surface.LogicalToDeviceUnits(bounds.Top + 10)));
                        var client = new NativePoint { X = screen.X, Y = screen.Y };
                        Require(ScreenToClient(hwnd, ref client), "Cannot map native selection pixels.");
                        nint dc = GetDC(hwnd);
                        Require(dc != 0, "Owned Designer DC unavailable.");
                        try
                        {
                            uint pixel = GetPixel(dc, client.X, client.Y);
                            var accent = DesignerColors.Accent;
                            Require(pixel != uint.MaxValue && (pixel & 255) == accent.Red && ((pixel >> 8) & 255) == accent.Green
                                && ((pixel >> 16) & 255) == accent.Blue, "The native frame did not contain the selection border.");
                        }
                        finally { ReleaseDC(hwnd, dc); }
                    }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("DESIGNER_GROUPS:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private sealed class VerificationForm : Form
    {
        internal ModernFormsDesignerShell? Shell { get; set; }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // The same pre-dispatch shortcut route used by DesignerPlayground's MainForm.
            if (Shell?.ProcessDesignerShortcut(e) == true) return;
            base.OnKeyDown(e);
        }
    }

    private static IEnumerable<Control> Descendants(Control owner)
    {
        // Locate widgets in the real shell's public control tree; do not create parallel views.
        foreach (Control child in owner.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static DesignControlNode Node(string name, DesignBounds bounds, string text)
    {
        var node = new DesignControlNode { TypeName = "Button", Name = name, Bounds = bounds };
        node.Properties["Text"] = DesignPropertyValue.FromString(text);
        return node;
    }

    private static void WithModifiers(Keys modifiers, Action action)
    {
        var original = new byte[256];
        Require(GetKeyboardState(original), "Cannot read UI-thread keyboard state.");
        var state = (byte[])original.Clone();
        foreach (int key in new[] { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C }) state[key] = 0;
        if (modifiers.HasFlag(Keys.Control)) state[0x11] = state[0xA2] = 0x80;
        if (modifiers.HasFlag(Keys.Shift)) state[0x10] = state[0xA0] = 0x80;
        try { Require(SetKeyboardState(state), "Cannot set UI-thread keyboard state."); action(); }
        finally { Require(SetKeyboardState(original), "Cannot restore UI-thread keyboard state."); }
    }

    private static nint Pack(NativePoint point) => (nint)((point.Y << 16) | (point.X & 0xffff));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(nint hwnd, nint rect, nint region, uint flags);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
}
