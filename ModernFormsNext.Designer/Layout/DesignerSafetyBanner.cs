using System.Drawing;
using ModernFormsNext.Designer.Services;
using SkiaSharp;

namespace ModernFormsNext.Designer.Layout;

/// <summary>
/// Shows one non-blocking recovery or external-change decision without interrupting editing or
/// repeating native filesystem notifications as modal prompts.
/// </summary>
internal sealed class DesignerSafetyBanner : Panel
{
    private readonly DesignerPersistenceCoordinator persistence;
    private readonly DesignerSession session;
    private readonly Label title;
    private readonly Label message;
    private readonly List<ActionButton> actionButtons = [];
    private DesignerPersistenceNotification? notification;

    public DesignerSafetyBanner(
        DesignerPersistenceCoordinator persistence,
        DesignerSession session)
    {
        this.persistence = persistence;
        this.session = session;
        Height = 68;
        Visible = false;
        Style.BackgroundColor = new SKColor(75, 61, 22);

        title = Controls.Add(new Label
        {
            Left = 12,
            Top = 8,
            Width = 500,
            Height = 20,
            Text = string.Empty
        });
        message = Controls.Add(new Label
        {
            Left = 12,
            Top = 31,
            Width = 680,
            Height = 28,
            AutoEllipsis = true,
            Text = string.Empty
        });
        title.Style.ForegroundColor = SKColors.White;
        message.Style.ForegroundColor = new SKColor(245, 232, 190);

        for (var index = 0; index < 6; index++)
        {
            var button = Controls.Add(new Button
            {
                Top = 18,
                Width = 94,
                Height = 30,
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter
            });
            button.Click += (_, _) => HandleAction(button);
            button.Style.BackgroundColor = new SKColor(103, 83, 27);
            button.Style.ForegroundColor = SKColors.White;
            button.Style.Border.Color = new SKColor(178, 145, 47);
            button.Style.Border.Width = 1;
            actionButtons.Add(new ActionButton(button));
        }

        persistence.StateChanged += Persistence_StateChanged;
        SizeChanged += (_, _) => LayoutBanner();
        RefreshNotice();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            persistence.StateChanged -= Persistence_StateChanged;
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Canvas.FillRectangle(0, 0, Width, Height, new SKColor(75, 61, 22));
        e.Canvas.DrawLine(0, Height - 1, Width, Height - 1, new SKColor(154, 125, 44));
        base.OnPaint(e);
    }

    private void Persistence_StateChanged(object? sender, EventArgs e)
        => RefreshNotice();

    private void RefreshNotice()
    {
        notification = persistence.CurrentNotification;
        Visible = notification is not null;
        if (notification is null)
        {
            foreach (var actionButton in actionButtons)
                actionButton.Button.Visible = false;
            return;
        }

        title.Text = notification.Title;
        message.Text = notification.Message;
        var actions = GetOrderedActions(notification.Actions).ToArray();
        for (var index = 0; index < actionButtons.Count; index++)
        {
            var actionButton = actionButtons[index];
            if (index >= actions.Length)
            {
                actionButton.Action = DesignerPersistenceActions.None;
                actionButton.Button.Visible = false;
                continue;
            }

            actionButton.Action = actions[index];
            actionButton.Button.Text = GetActionText(notification.Kind, actions[index]);
            actionButton.Button.Width = Math.Max(94, (actionButton.Button.Text.Length * 8) + 20);
            actionButton.Button.Visible = true;
        }

        LayoutBanner();
        Invalidate();
    }

    private void LayoutBanner()
    {
        var right = Width - 10;
        for (var index = actionButtons.Count - 1; index >= 0; index--)
        {
            var button = actionButtons[index].Button;
            if (!button.Visible)
                continue;
            right -= button.Width;
            button.Left = right;
            right -= 7;
        }

        var textWidth = Math.Max(160, right - 18);
        title.Width = textWidth;
        message.Width = textWidth;
    }

    private async void HandleAction(Button button)
    {
        var actionButton = actionButtons.FirstOrDefault(candidate => ReferenceEquals(candidate.Button, button));
        var current = notification;
        if (actionButton is null || current is null)
            return;

        if (actionButton.Action == DesignerPersistenceActions.Compare)
        {
            if (FindForm() is { } owner)
            {
                using var dialog = new DesignerComparisonDialog(
                    current.DocumentName,
                    persistence.GetCurrentComparisonText(current.Id));
                await dialog.ShowDialog(owner);
            }
            return;
        }

        string? saveAsPath = null;
        if (actionButton.Action == DesignerPersistenceActions.SaveAs)
        {
            saveAsPath = await SelectSavePath(current.DocumentName);
            if (string.IsNullOrWhiteSpace(saveAsPath))
                return;
        }

        if (!persistence.ApplyCurrentAction(current.Id, actionButton.Action, saveAsPath, out var error)
            && !string.IsNullOrWhiteSpace(error))
        {
            session.Log(error);
        }
    }

    private async Task<string?> SelectSavePath(string suggestedName)
    {
        if (FindForm() is not { } owner)
            return null;

        var dialog = new SaveFileDialog
        {
            Title = "Save recovered Designer document as",
            DefaultExtension = "mfdesign",
            FileName = suggestedName
        };
        dialog.AddFilter("ModernFormsNext design files", "*.mfdesign");
        dialog.AddFilter("All files", "*.*");
        return await dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
    }

    private static IEnumerable<DesignerPersistenceActions> GetOrderedActions(DesignerPersistenceActions actions)
    {
        var order = new[]
        {
            DesignerPersistenceActions.Restore,
            DesignerPersistenceActions.OpenDisk,
            DesignerPersistenceActions.Reload,
            DesignerPersistenceActions.Keep,
            DesignerPersistenceActions.SaveAs,
            DesignerPersistenceActions.Discard,
            DesignerPersistenceActions.Compare,
            DesignerPersistenceActions.Dismiss
        };
        return order.Where(action => (actions & action) != 0).Take(6);
    }

    internal static string GetActionText(
        DesignerPersistenceNoticeKind noticeKind,
        DesignerPersistenceActions action)
    {
        var recoveryNotice = noticeKind is DesignerPersistenceNoticeKind.RecoveryAvailable
            or DesignerPersistenceNoticeKind.RecoveryConflict;
        return action switch
        {
            DesignerPersistenceActions.Restore => "Restore",
            DesignerPersistenceActions.Discard => recoveryNotice ? "Discard Recovery" : "Discard",
            DesignerPersistenceActions.Keep => recoveryNotice ? "Keep Recovery" : "Keep Designer",
            DesignerPersistenceActions.Reload => "Reload",
            DesignerPersistenceActions.SaveAs => "Save As",
            DesignerPersistenceActions.OpenDisk => "Open Disk",
            DesignerPersistenceActions.Compare => "Compare",
            DesignerPersistenceActions.Dismiss => "Dismiss",
            _ => string.Empty
        };
    }

    private sealed class ActionButton(Button button)
    {
        public Button Button { get; } = button;

        public DesignerPersistenceActions Action { get; set; }
    }
}

/// <summary>
/// Presents a read-only fingerprint and timestamp comparison for a recovery or external-change
/// decision. It intentionally does not execute or merge source code.
/// </summary>
internal sealed class DesignerComparisonDialog : Form
{
    public DesignerComparisonDialog(string documentName, string comparisonText)
    {
        Text = $"Compare {documentName}";
        Name = nameof(DesignerComparisonDialog);
        Size = new Size(720, 420);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new TextBox
        {
            Left = 20,
            Top = 20,
            Width = 665,
            Height = 310,
            MultiLine = true,
            ReadOnly = true,
            Text = comparisonText
        });
        var close = Controls.Add(new Button
        {
            Left = 600,
            Top = 342,
            Width = 84,
            Height = 30,
            Text = "Close",
            TextAlign = ContentAlignment.MiddleCenter
        });
        close.Click += (_, _) => DialogResult = DialogResult.OK;
    }
}
