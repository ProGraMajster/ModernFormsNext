using System.Drawing;
using ModernFormsNext;

using var form = new Form
{
    ClientSize = new Size(480, 240),
    Text = "ModernFormsNext UIA integration window"
};

var button = new Button
{
    AccessibleAutomationId = "uia.integration.invoke",
    AccessibleName = "Invoke integration action",
    Bounds = new Rectangle(24, 24, 220, 48),
    Text = "Invoke"
};

button.Click += (_, _) =>
{
    form.Text = "ModernFormsNext UIA action invoked";
    Console.WriteLine("INVOKED");
};

form.Controls.Add(button);
form.Shown += (_, _) =>
{
    Console.WriteLine($"HWND:{form.PlatformHandle.Handle.ToInt64()}");
};

Application.Run(form);
