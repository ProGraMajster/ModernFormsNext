using System.Drawing;
using System.Runtime.InteropServices;
using ModernFormsNext;
using ModernFormsNext.Animations;
using ModernFormsNext.Testing;
using SkiaSharp;

int expectedRuntime = int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture);
Require(Environment.Version.Major == expectedRuntime, "The app is running on the requested CLR.");
Console.WriteLine($"Runtime={RuntimeInformation.FrameworkDescription}; NativeSkia={SkiaSharpVersion.Native}");

using var host = ModernFormsTestHost.Create();
var root = new Panel { BackColor = SKColors.Navy };
var button = root.Controls.Add(new Button { Text = "Save", Bounds = new(10, 10, 100, 32) });
var editor = root.Controls.Add(new TextBox { Bounds = new(10, 56, 110, 32) });
var window = host.Show(root, new TestViewport(240, 120, 1.25));
window.LayoutUntilStable();
int clicks = 0;
button.Click += (_, _) => clicks++;

var contact = new Point(40, 22);
window.Input.PointerDown(contact);
Require(button.Capture, "Mouse down took capture.");
window.Input.LoseCapture();
window.Input.PointerUp(contact);
Require(!button.Capture && clicks == 0, "Capture loss discarded the outstanding mouse press.");
window.Input.Touch(7, ControlSurfacePointerAction.Down, contact);
Require(button.Capture, "Simulated touch went through the hosted surface.");
window.Input.Touch(7, ControlSurfacePointerAction.Cancel, contact);
window.Input.Touch(7, ControlSurfacePointerAction.Up, contact);
Require(!button.Capture && clicks == 0, "Cancellation retired the identified contact.");
window.Input.Touch(7, ControlSurfacePointerAction.Down, contact);
window.Input.Touch(7, ControlSurfacePointerAction.Up, contact);
Require(clicks == 1 && ReferenceEquals(window.FocusedControl, button), "A fresh contact activated the button.");
window.Input.Tab();
Require(ReferenceEquals(window.FocusedControl, editor), "Normal Tab navigation focuses the editor.");
window.Input.TextInput("Zażółć 123");
Require(editor.Text == "Zażółć 123", "Committed text reached the focused editor.");

AnimationScheduler.Default.Start(button, "opacity", progress => button.Opacity = progress,
    new AnimationOptions { Duration = TimeSpan.FromSeconds(1), Easing = Easings.Linear });
host.Clock.Advance(TimeSpan.FromMilliseconds(500));
Require(button.Opacity == 0.5f, "The production scheduler advanced to the halfway frame.");
using var image = window.CaptureRenderedSnapshot();
Require(image.PixelWidth == 300 && image.PixelHeight == 150, "The frame uses scaled raster dimensions.");
Require(image.GetPixel(280, 140) == SKColors.Navy, "Background pixels were produced by the real renderer.");
Require(window.CaptureTree().Bounds.Size == new Size(240, 120), "Layout stayed in logical coordinates.");
Require(host.GetDiagnostics().DispatcherExceptions.Count == 0, "The dispatcher reported no unhandled work.");
window.Close();
Require(host.Windows.Count == 0 && window.IsClosed, "Closing retired the hosted tree.");
Console.WriteLine("TestHost package smoke passed. Input, focus, clock, layout, raster and cleanup.");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
