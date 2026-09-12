using ModernFormsNext;
using ModernFormsNext.WindowKit.Input;
using SkiaSharp;

// The normal test host owns an actual framework HWND and its actual documents. The client uses
// OS UI Automation only; no mirrored document, in-process provider access or physical input.
internal static class TextScenario
{
    internal static void Run()
    {
        using var form = new Form { ClientSize = new(680, 360), Text = "ModernFormsNext native Text integration" };
        var editor = form.Controls.Add(new RichTextBox {
            AccessibleAutomationId = "uia.text.editor", AccessibleName = "Document",
            Bounds = new(20, 20, 360, 160), MultiLine = true,
            Text = "alpha 😀 beta\n" + string.Join("\n", Enumerable.Range(0, 40).Select(i => "line " + i)) + "\nomega" });
        editor.Select(0, 5);
        editor.SelectionColor = SKColors.Blue;
        editor.Select(0, 0);
        var readOnly = form.Controls.Add(new TextBox {
            AccessibleAutomationId = "uia.text.readonly", AccessibleName = "Read-only document",
            Bounds = new(20, 210, 360, 50), ReadOnly = true, Text = "read-only document" });
        var focus = form.Controls.Add(new Button {
            AccessibleAutomationId = "uia.text.focus", Text = "Retain focus", Bounds = new(420, 20, 220, 35) });
        var append = form.Controls.Add(new Button {
            AccessibleAutomationId = "uia.text.append", Text = "Append", Bounds = new(420, 70, 220, 35) });
        append.Click += (_, _) => editor.AppendText(" tail");
        var protect = form.Controls.Add(new Button {
            AccessibleAutomationId = "uia.text.protect", Text = "Protect document", Bounds = new(420, 120, 220, 35) });
        protect.Click += (_, _) => editor.TextInputOptions = editor.TextInputOptions with { Scope = TextInputScope.Password };
        var remove = form.Controls.Add(new Button {
            AccessibleAutomationId = "uia.text.remove", Text = "Remove read-only document", Bounds = new(420, 170, 220, 35) });
        remove.Click += (_, _) => { form.Controls.Remove(readOnly); readOnly.Dispose(); };
        form.Shown += (_, _) => { focus.Select(); Console.WriteLine($"HWND:{form.PlatformHandle.Handle.ToInt64()}"); };
        Application.Run(form);
    }
}
