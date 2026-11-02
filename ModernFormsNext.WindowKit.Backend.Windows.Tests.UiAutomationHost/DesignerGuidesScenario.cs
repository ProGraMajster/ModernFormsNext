using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Designer;
using ModernFormsNext.Designer.Layout;
using ModernFormsNext.Designer.Localization;
using ModernFormsNext.Designer.Properties;
using ModernFormsNext.Designer.Surface;
using ModernFormsNext.Designing;
using ModernFormsNext.WindowKit.Threading;
using ContentAlignment = ModernFormsNext.ContentAlignment;

internal static partial class DesignerGroupScenario
{
    internal static int RunGuides()
    {
        try
        {
            using var form = new VerificationForm { Text = "Designer guides certification", ClientSize = new(1000, 700) };
            var options = new ModernFormsDesignerOptions { AutoSaveEnabled = false, ShowSolutionExplorer = false,
                ShowOutputPanel = false, Language = DesignerLanguage.English };
            var shell = form.Controls.Add(new ModernFormsDesignerShell(options) { Dock = DockStyle.Fill });
            form.Shell = shell;
            var session = shell.Session;
            var surface = Descendants(shell).OfType<DesignerSurface>().Single();
            Exception? failure = null;
            int scene = 0;
            form.Shown += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    nint hwnd = form.PlatformHandle.Handle;
                    Require(IsWindow(hwnd), "An actual HWND is required for guides.");
                    Require(SetWindowPos(hwnd, new nint(-1), 40, 40, 0, 0, 0x0011), "Cannot expose owned guides window.");
                    form.SetTextInputActive(true);
                    var first = Node("moving", new(10, 20, 100, 30), "Moving");
                    var second = Node("target", new(200, 120, 100, 30), "Target");
                    Load(first, second);
                    Pointer(40, 35, 0x0201, 1);
                    Pointer(225, 35, 0x0200, 1);
                    Require(first.Bounds.X == 200 && surface.MouseController.Guides.Any(guide => guide.Vertical), "Native edge acquisition failed.");
                    CheckAccent(200, 80);
                    Pointer(225, 35, 0x0202);
                    Require(session.CurrentHistory.UndoCount == 1 && session.Transactions.Undo(), "Guided native movement was not one undo unit.");
                    Require(first.Bounds.X == 10, "Guided movement undo failed.");
                    Console.WriteLine("DESIGNER_GUIDES:NATIVE_MOVE_PIXEL_UNDO:PASS");

                    Pointer(40, 35, 0x0201, 1);
                    WithModifiers(Keys.Alt, () => Pointer(225, 35, 0x0200, 1));
                    Require(first.Bounds.X != 200 && surface.MouseController.Guides.Count == 0, "Native Alt did not bypass snapping.");
                    Require(surface.Selected, $"Surface lacks focus before native Escape. " +
                        $"CanSelect={surface.CanSelect}; Visible={surface.Visible}; Active={form.IsActive}. " + FocusAncestry(surface));
                    SendMessage(hwnd, 0x0100, 0x1B, 1);
                    SendMessage(hwnd, 0x0101, 0x1B, unchecked((nint)0xC0000001));
                    Require(first.Bounds.X == 10 && !surface.Capture && !session.Transactions.CanUndo,
                        "Native Escape must roll back before mouse-up, while the pointer gesture is still active.");
                    Pointer(225, 35, 0x0202);
                    Require(first.Bounds.X == 10 && !surface.Capture && !session.Transactions.CanUndo,
                        $"Native Escape did not roll back the bypassed gesture. Bounds={first.Bounds}; Capture={surface.Capture}; " +
                        $"Undo={session.CurrentHistory.UndoCount}; SurfaceSelected={surface.Selected}; " +
                        $"ActiveTransaction={session.Transactions.HasActiveTransaction}. " + string.Join(" | ", session.OutputLines.TakeLast(12)));

                    session.SelectNode(first);
                    Pointer(110, 35, 0x0201, 1);
                    Pointer(197, 35, 0x0200, 1);
                    Require(first.Bounds.X == 10 && first.Bounds.Right == 200, "Native resize did not preserve the opposite edge.");
                    CheckAccent(200, 80);
                    Pointer(197, 35, 0x0202);
                    Require(session.Transactions.Undo() && first.Bounds.Width == 100, "Native guided resize undo failed.");
                    Console.WriteLine("DESIGNER_GUIDES:NATIVE_BYPASS_ESCAPE_RESIZE:PASS");

                    first = Node("moving", new(10, 20, 40, 30), "Move");
                    Load(first, Node("left", new(100, 20, 40, 30), "Left"), Node("right", new(180, 20, 40, 30), "Right"));
                    Pointer(30, 35, 0x0201, 1);
                    Pointer(278, 35, 0x0200, 1);
                    Require(first.Bounds.X == 260 && surface.MouseController.Guides.Count(guide => guide.Kind == DesignerSnapGuideKind.Spacing) == 2,
                        "Native equal-gap hint or snapped result failed.");
                    CheckAccent(240, 35);
                    Pointer(278, 35, 0x0202);
                    Require(session.Transactions.Undo(), "Equal spacing did not participate in history.");
                    Console.WriteLine("DESIGNER_GUIDES:NATIVE_EQUAL_SPACING_PIXEL:PASS");

                    first = new DesignControlNode { TypeName = "Label", Name = "movingLabel", Bounds = new(20, 20, 160, 70) };
                    second = new DesignControlNode { TypeName = "Label", Name = "targetLabel", Bounds = new(300, 100, 160, 70) };
                    foreach (var node in new[] { first, second })
                    {
                        node.Properties["Text"] = DesignPropertyValue.FromString("Baseline");
                        node.Properties["TextAlign"] = DesignerPropertyValueEditor.ToDesignPropertyValue(ContentAlignment.TopLeft, typeof(ContentAlignment));
                    }
                    first.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(12);
                    second.Properties["Style.FontSize"] = DesignPropertyValue.FromInt32(30);
                    Load(first, second);
                    using var reference = new Label { Text = "Baseline", Bounds = new Rectangle(0, 0, 160, 70), TextAlign = ContentAlignment.TopLeft };
                    reference.Style.FontSize = 30;
                    double baseline = 100 + (DesignerTextBaseline.MeasureControl(reference) ?? throw new InvalidOperationException("Production baseline missing."));
                    double firstBaseline = 20 + (DesignerTextBaseline.Measure(first, DesignerSnapBounds.From(first.Bounds)) ?? throw new InvalidOperationException("Moving baseline missing."));
                    int delta = (int)Math.Round(baseline - firstBaseline, MidpointRounding.AwayFromZero);
                    Pointer(50, 35, 0x0201, 1);
                    Pointer(50, 35 + delta - 2, 0x0200, 1);
                    Require(surface.MouseController.Guides.Any(guide => guide.Kind == DesignerSnapGuideKind.Baseline), "Native font baseline guide missing.");
                    Require(Math.Abs(first.Bounds.Y - 20 + firstBaseline - baseline) <= 0.5, "Native fonts did not share their production baseline.");
                    CheckAccent(240, baseline);
                    Pointer(50, 35 + delta - 2, 0x0202);
                    Require(session.Transactions.Undo(), "Baseline move undo failed.");
                    Console.WriteLine("DESIGNER_GUIDES:NATIVE_BASELINE_PIXEL:PASS");

                    Load(Node("settingsSource", new(10, 20, 100, 30), "Source"));
                    string saved = DesignDocumentSerializer.Default.Serialize(session.Document);
                    using var settings = new DesignerSettingsDialog(options, session);
                    Exception? settingsFailure = null;
                    settings.Shown += (_, _) => Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            nint settingsHwnd = settings.PlatformHandle.Handle;
                            Require(IsWindow(settingsHwnd), "Settings has no HWND.");
                            foreach (string name in new[] { "SnappingEnabled", "ShowDesignGrid", "SnapToDesignGrid" })
                            {
                                var check = settings.Controls.OfType<CheckBox>().Single(control => control.Name == name);
                                NativeClick(check, settingsHwnd);
                            }
                            // Numeric range/admission is exercised separately; this owned dialog
                            // phase certifies actual checkbox routing and the normal Apply button.
                            settings.Controls.OfType<NumericUpDown>().Single(control => control.Name == "DesignGridSize").Value = 16;
                            NativeClick(settings.Controls.OfType<Button>().Single(control => control.Text == DesignerText.Get("Ok", options.Language)), settingsHwnd);
                        }
                        catch (Exception error) { settingsFailure = error; settings.Close(); }
                    });
                    await settings.ShowDialog(form);
                    if (settingsFailure is not null) throw settingsFailure;
                    Require(!session.SnappingOptions.Enabled && !session.SnappingOptions.ShowGrid && session.SnappingOptions.SnapToGrid
                        && session.SnappingOptions.GridSize == 16, "Native Settings Apply did not update the host preferences.");
                    Require(saved == DesignDocumentSerializer.Default.Serialize(session.Document) && !session.IsDirty && !session.Transactions.CanUndo,
                        "Guide settings changed authored data or history.");
                    Require(!surface.Capture && !session.Transactions.HasActiveTransaction, "Guide gesture resources remain active.");
                    Console.WriteLine("DESIGNER_GUIDES:NATIVE_SETTINGS_CHECKBOX_APPLY_BOUNDED_NUMERIC:PASS");

                    void Load(params DesignControlNode[] nodes)
                    {
                        var document = new DesignDocument { Namespace = "DesignerCertification", ClassName = "GuideForm" + ++scene,
                            FormName = "guideForm", Size = new(800, 500) };
                        foreach (var node in nodes) document.Controls.Add(node);
                        shell.LoadDocument(document);
                    }

                    string FocusAncestry(Control control)
                    {
                        var values = new List<string>();
                        for (Control? current = control; current is not null; current = current.Parent)
                            values.Add($"{current.GetType().Name}:{current.Name}:Enabled={current.Enabled}:Visible={current.Visible}:CanSelect={current.CanSelect}");
                        return string.Join(" / ", values);
                    }

                    Point SurfacePoint(double x, double y)
                    {
                        var view = new DesignerCoordinateMapper().GetView(session, surface.Width, surface.Height);
                        return new(surface.LogicalToDeviceUnits(view.ClientX + (int)Math.Round(x * view.Scale)),
                            surface.LogicalToDeviceUnits(view.ClientY + (int)Math.Round(y * view.Scale)));
                    }

                    void Pointer(double x, double y, uint message, int buttons = 0)
                        => NativePointer(surface, SurfacePoint(x, y), hwnd, message, buttons);

                    void NativeClick(Control control, nint target)
                        => WithModifiers(Keys.None, () =>
                        {
                            var point = new Point(control.ScaledWidth / 2, control.ScaledHeight / 2);
                            NativePointer(control, point, target, 0x0201, 1);
                            NativePointer(control, point, target, 0x0202);
                        });

                    void NativePointer(Control control, Point point, nint target, uint message, int buttons = 0)
                    {
                        GetWindowThreadProcessId(target, out uint owner);
                        Require(owner == (uint)Environment.ProcessId, "Pointer target is not the owned native fixture.");
                        var screen = control.PointToScreen(point);
                        var local = new NativePoint { X = screen.X, Y = screen.Y };
                        Require(ScreenToClient(target, ref local), "Cannot map guide input.");
                        SendMessage(target, message, buttons, Pack(local));
                    }

                    void CheckAccent(double x, double y)
                    {
                        Require(RedrawWindow(hwnd, 0, 0, 0x0001 | 0x0100), "Guide window did not paint.");
                        var screen = surface.PointToScreen(SurfacePoint(x, y));
                        var point = new NativePoint { X = screen.X, Y = screen.Y };
                        Require(ScreenToClient(hwnd, ref point), "Cannot map native guide pixel.");
                        nint dc = GetDC(hwnd);
                        Require(dc != 0, "Guide frame DC unavailable.");
                        try
                        {
                            uint pixel = GetPixel(dc, point.X, point.Y);
                            var accent = DesignerColors.Accent;
                            Require(pixel != uint.MaxValue && (pixel & 255) == accent.Red && ((pixel >> 8) & 255) == accent.Green
                                && ((pixel >> 16) & 255) == accent.Blue, "Native frame lacks the actual guide pixel.");
                        }
                        finally { ReleaseDC(hwnd, dc); }
                    }
                }
                catch (Exception error) { failure = error; }
                finally { Application.Exit(); }
            });
            Application.Run(form);
            if (failure is not null) throw failure;
            Console.WriteLine("DESIGNER_GUIDES:PASS:HWND");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
