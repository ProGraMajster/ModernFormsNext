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

bool clicked = false;
button.Click += (_, _) => clicked = true;
button.CommandParameter = "uia-command";
button.Command = new DelegateCommand(parameter =>
{
    // The real cross-process UIA client must reach Click before the parameterized command.
    if (!clicked || !Equals(parameter, "uia-command"))
        throw new InvalidOperationException("UIA did not use the normal Button command path.");
    form.Text = "ModernFormsNext UIA action invoked";
    Console.WriteLine("INVOKED");
});

form.Controls.Add(button);
form.Controls.Add(new Button {
    AccessibleAutomationId = "uia.integration.disabled-command",
    Text = "Unavailable command",
    Bounds = new Rectangle(24, 90, 220, 48),
    Command = new DelegateCommand(() => throw new InvalidOperationException("Unavailable command invoked."), () => false)
});
form.Shown += (_, _) =>
{
    Console.WriteLine($"HWND:{form.PlatformHandle.Handle.ToInt64()}");
};

Application.Run(form);
