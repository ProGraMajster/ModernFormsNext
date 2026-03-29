using System;
using ModernFormsNext;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "ModernFormsNext App";

        var button = new Button
        {
            Text = "Click me",
            Left = 20,
            Top = 20,
            Width = 120,
            Height = 40
        };

        button.Click += (_, _) =>
        {
            Text = "Button clicked";
        };

        Controls.Add(button);
    }
}