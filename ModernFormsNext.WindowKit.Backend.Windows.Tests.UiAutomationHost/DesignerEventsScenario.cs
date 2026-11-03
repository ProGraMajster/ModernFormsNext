using System.Drawing;
using System.Reflection;
using ModernFormsNext;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.WindowKit.Threading;

internal static partial class DesignerGroupScenario
{
    // Actual routed HWND pointer/keyboard input exercises the production shell. The injected code
    // host is an asynchronous in-memory buffer, not a claim of real Visual Studio navigation.
    internal static int RunEvents()
    {
        try
        {
            var environment = new EventCodeHost();
            using var form = new VerificationForm { Text = "Designer event certification", ClientSize = new(1000, 700) };
            var shell = form.Controls.Add(new ModernFormsDesignerShell(new ModernFormsDesignerOptions {
                AutoSaveEnabled = false, ShowSolutionExplorer = false, ShowOutputPanel = false,
                Language = DesignerLanguage.English }, environment) { Dock = DockStyle.Fill });
            form.Shell = shell;
            var session = shell.Session;
            var first = Node("firstButton", new(20, 30, 100, 34), "First");
            var document = new DesignDocument { Namespace = "DesignerCertification", ClassName = "EventForm", FormName = "eventForm", Size = new(480, 360) };
            document.Controls.Add(first);
            session.OpenDocument(document, environment.CurrentDocumentPath);
            var surface = Descendants(shell).OfType<DesignerSurface>().Single();
            var grid = Descendants(shell).OfType<DesignerPropertyGrid>().Single();
            var state = (DesignerPropertyGridState)typeof(DesignerPropertyGrid).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(grid)!;
            Exception? failure = null;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    var hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "Actual Designer event HWND missing.");
                    Require(SetWindowPos(hwnd, new nint(-1), 40, 40, 0, 0, 0x0011), "Cannot expose owned event window.");
                    form.SetTextInputActive(true);
                    var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
                    var buttonPoint = new Point(surface.LogicalToDeviceUnits(view.ClientX + (int)(50 * view.Scale)),
                        surface.LogicalToDeviceUnits(view.ClientY + (int)(45 * view.Scale)));
                    Double(surface, buttonPoint, hwnd);
                    await Until(() => first.Events.GetValueOrDefault("Click") == "firstButton_Click" && environment.Navigations == 1);
                    Require(environment.Insertions == 1 && session.CurrentHistory.UndoCount == 1
                        && !session.Transactions.HasActiveTransaction && !surface.Capture, "Default event must commit once after its async source edit.");
                    var createdSource = environment.Source;
                    Console.WriteLine("DESIGNER_EVENTS:DEFAULT_DOUBLE_ASYNC_BIND_NAVIGATE:PASS");
                    Double(surface, buttonPoint, hwnd);
                    await Until(() => environment.Navigations == 2);
                    Require(environment.Insertions == 1 && session.CurrentHistory.UndoCount == 1, "Repeated activation duplicated a method or binding.");
                    EventValueDouble();
                    await Until(() => environment.Navigations == 3);
                    Console.WriteLine("DESIGNER_EVENTS:FIELD_DOUBLE_EXISTING_NO_DUPLICATE:PASS");

                    EditName("Compatible");
                    await Until(() => first.Events.GetValueOrDefault("Click") == "Compatible");
                    Require(environment.Source == createdSource && environment.Insertions == 1, "Assigning an existing method changed user source.");
                    Console.WriteLine("DESIGNER_EVENTS:NATIVE_ENTER_COMPATIBLE_BIND:PASS");

                    grid.Select();
                    Key(0x0D);
                    await Until(() => Descendants(grid).OfType<ComboBox>().Any(b => b.AccessibleName == "Choose compatible event handler" && b.Enabled));
                    var chooser = Descendants(grid).OfType<ComboBox>().Single(b => b.AccessibleName == "Choose compatible event handler");
                    Require(chooser.Items.Cast<object>().Any(i => i.ToString() == "Compatible")
                        && !chooser.Items.Cast<object>().Any(i => i.ToString() == "Wrong"), "Chooser included an incompatible method.");
                    Click(chooser, new(chooser.ScaledWidth / 2, chooser.ScaledHeight / 2), hwnd);
                    var popupField = typeof(ComboBox).GetField("popup", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    await Until(() => popupField.GetValue(chooser) is PopupWindow { Visible: true });
                    var popup = (PopupWindow)popupField.GetValue(chooser)!;
                    var list = (ListBox)typeof(ComboBox).GetField("popup_listbox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chooser)!;
                    var clear = list.GetItemRectangle(1);
                    Click(list, new(clear.Left + clear.Width / 2, clear.Top + clear.Height / 2), popup.PlatformHandle.Handle);
                    await Until(() => string.IsNullOrEmpty(first.Events.GetValueOrDefault("Click")));
                    Require(environment.Source == createdSource && session.CurrentHistory.UndoCount == 3, "Unbind did not preserve source or atomic history.");
                    WithModifiers(Keys.Control, () => Key(0x5A));
                    Require(first.Events["Click"] == "Compatible" && environment.Source == createdSource, "Native Undo deleted source or failed to restore binding.");
                    Console.WriteLine("DESIGNER_EVENTS:NATIVE_CHOOSER_CLEAR_UNDO_PRESERVES_CODE:PASS");

                    environment.Source = environment.Source.Replace("void Compatible(", "void Renamed(", StringComparison.Ordinal);
                    grid.RefreshProperties();
                    await Until(() => state.SelectedEvent?.Diagnostic?.Contains("missing or was renamed", StringComparison.Ordinal) == true);
                    Require(first.Events["Click"] == "Compatible" && environment.Insertions == 1, "Diagnostics mutated a broken binding.");
                    Console.WriteLine("DESIGNER_EVENTS:MISSING_HANDLER_DIAGNOSTIC_PRESERVES_BINDING:PASS");
                    Require(RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100), "Designer event window failed to paint.");

                    void EditName(string name)
                    {
                        grid.Select(); Key(0x0D);
                        Require(Descendants(grid).OfType<TextBox>().Any(), "Native Enter did not open the event editor.");
                        foreach (var character in name)
                        {
                            // A translated character starts a new key sequence. Enter consumed
                            // the previous sequence, so sending WM_CHAR alone would be suppressed.
                            SendMessage(hwnd, 0x0100, 0xE7, 1); // VK_PACKET, as for Unicode input.
                            SendMessage(hwnd, 0x0102, character, 1);
                            SendMessage(hwnd, 0x0101, 0xE7, unchecked((nint)0xC0000001));
                        }
                        Require(Descendants(grid).OfType<TextBox>().Single().Text == name,
                            "Native translated text did not replace the selected handler name.");
                        Key(0x0D);
                    }
                    void EventValueDouble()
                    {
                        var y = DesignerPropertyGridMetrics.GridTop;
                        foreach (var row in state.Rows)
                        {
                            if (row.Event?.Name == "Click")
                            { Double(grid, new(grid.LogicalToDeviceUnits(grid.Width - 50), grid.LogicalToDeviceUnits(y + 12)), hwnd); return; }
                            y += row.Kind == DesignerPropertyGridRowKind.Category ? DesignerPropertyGridMetrics.CategoryRowHeight : DesignerPropertyGridMetrics.RowHeight;
                        }
                        throw new InvalidOperationException("Click event row missing.");
                    }
                    void Key(int key)
                    { SendMessage(hwnd, 0x0100, key, 1); SendMessage(hwnd, 0x0101, key, unchecked((nint)0xC0000001)); }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("DESIGNER_EVENTS:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }

        static async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline) throw new InvalidOperationException("Native event condition did not complete within four seconds.");
                await Task.Delay(10);
            }
        }
        static void Click(Control control, Point point, nint hwnd)
        { Pointer(control, point, hwnd, 0x0201, 1); Pointer(control, point, hwnd, 0x0202, 0); }
        static void Double(Control control, Point point, nint hwnd)
        { Click(control, point, hwnd); Pointer(control, point, hwnd, 0x0203, 1); Pointer(control, point, hwnd, 0x0202, 0); }
        static void Pointer(Control control, Point point, nint hwnd, uint message, int buttons)
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            Require(owner == (uint)Environment.ProcessId, "Event input must target this fixture's HWND.");
            var screen = control.PointToScreen(point);
            var local = new NativePoint { X = screen.X, Y = screen.Y };
            Require(ScreenToClient(hwnd, ref local), "Cannot map native event pointer.");
            WithModifiers(Keys.None, () => SendMessage(hwnd, message, buttons, Pack(local)));
        }
    }

    private sealed class EventCodeHost : IDesignerHostEnvironment, IDesignerCodeDocumentHostEnvironment
    {
        public EventCodeHost()
        {
            // The shell's existing project explorer and external-change watcher require a real
            // document directory even though this fixture supplies its source through memory.
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ModernFormsNext-event-native", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            CurrentDocumentPath = System.IO.Path.Combine(directory, "EventForm.mfdesign");
        }

        public string? CurrentDocumentPath { get; }
        public string? CurrentProjectPath => null;
        internal string Source = "namespace DesignerCertification; public partial class EventForm : ModernFormsNext.Form { private void Compatible(object? s, global::System.EventArgs e) { } private int Wrong(object? s, global::System.EventArgs e) => 1; }";
        internal int Insertions;
        internal int Navigations;
        public void ReportStatus(string message) { }
        public void ReportOutput(string message) => Console.WriteLine("DESIGNER_EVENT_LOG:" + message);
        public async Task<string?> ReadCodeDocumentAsync(string path, CancellationToken cancellationToken)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return Source; }
        public async Task<bool> InsertCodeAsync(string path, string expectedText, int insertionOffset, string insertedText, CancellationToken cancellationToken)
        {
            await Task.Yield(); cancellationToken.ThrowIfCancellationRequested();
            if (Source != expectedText) return false;
            Source = Source.Insert(insertionOffset, insertedText); Insertions++; return true;
        }
        public Task<bool> NavigateToCodeAsync(string path, string expectedText, int offset, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Require(Source == expectedText && offset > 0 && offset <= Source.Length, "Host received stale navigation coordinates.");
            Navigations++; return Task.FromResult(true);
        }
    }
}
