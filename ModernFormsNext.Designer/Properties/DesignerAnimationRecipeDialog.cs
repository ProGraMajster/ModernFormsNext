using System.ComponentModel;
using System.Drawing;
using ModernFormsNext.Animations;
using ModernFormsNext.Designing;
using ModernFormsNext.WindowKit.Threading;

namespace ModernFormsNext.Designer.Properties;

// The draft and preview target live only in this dialog. The normal property grid transaction
// receives the validated value after OK, so playing or stopping cannot dirty the document.
internal sealed class DesignerAnimationRecipeDialog : Form
{
    private const string Example = "Click\nScale 0.9 160 EaseOut\nScale 1 240 BlendEaseInOut 0.6";
    private static readonly TimeSpan PreviewDeadline = TimeSpan.FromSeconds(3);
    private readonly TextBox input;
    private readonly Label status;
    private readonly Button sample;
    private AnimationRun? run;
    private CancellationTokenSource? deadline;

    internal DesignerAnimationRecipeDialog(string? recipe)
    {
        Name = nameof(DesignerAnimationRecipeDialog);
        Text = "Animation Recipe";
        Size = new Size(720, 445);
        MinimumSize = new Size(720, 445);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label
        {
            Bounds = new Rectangle(16, 16, 672, 48),
            Text = "Start with Click. Put the property, target value, duration in ms and easing on each following line.\n"
                + "Use Opacity, Scale or Rotation. BlendEaseInOut takes a final strength from 0 to 1."
        });
        input = Controls.Add(new TextBox
        {
            Name = "AnimationRecipeText",
            Bounds = new Rectangle(16, 72, 418, 174),
            MultiLine = true,
            MaxLength = AnimationRecipeDesignValue.MaximumLength,
            Text = recipe ?? Example,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        });
        var sampleArea = Controls.Add(new Panel
        {
            Bounds = new Rectangle(446, 72, 242, 174),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        });
        sample = sampleArea.Controls.Add(new Button
        {
            Name = "AnimationRecipeSample",
            Bounds = new Rectangle(72, 66, 98, 42),
            Text = "Sample",
            TabStop = false
        });
        var play = AddButton("Play", 16, 258, AnchorStyles.Top | AnchorStyles.Left);
        var stop = AddButton("Stop", 110, 258, AnchorStyles.Top | AnchorStyles.Left);
        var restart = AddButton("Restart", 204, 258, AnchorStyles.Top | AnchorStyles.Left);
        status = Controls.Add(new Label
        {
            Name = "AnimationRecipeStatus",
            Bounds = new Rectangle(16, 302, 672, 42),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        });
        var remove = AddButton("Remove", 16, 352, AnchorStyles.Left | AnchorStyles.Bottom);
        var ok = AddButton("OK", 506, 352, AnchorStyles.Right | AnchorStyles.Bottom);
        var cancel = AddButton("Cancel", 600, 352, AnchorStyles.Right | AnchorStyles.Bottom);

        input.TextChanged += (_, _) => { StopPreview(); UpdateStatus(); };
        play.Click += async (_, _) =>
        {
            if (run is not { State: AnimationState.Running })
                await PlayAsync();
        };
        stop.Click += (_, _) => { StopPreview(); UpdateStatus(); };
        restart.Click += async (_, _) => await PlayAsync();
        remove.Click += (_, _) => { Recipe = null; DialogResult = DialogResult.OK; };
        ok.Click += (_, _) =>
        {
            if (!AnimationRecipeDesignValue.TryValidate(input.Text, out string? error))
            {
                status.Text = error!;
                return;
            }
            Recipe = input.Text;
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        UpdateStatus();
    }

    internal string? Recipe { get; private set; }

    private Button AddButton(string text, int left, int top, AnchorStyles anchor)
        => Controls.Add(new Button
        {
            Name = "AnimationRecipe" + text,
            Bounds = new Rectangle(left, top, 88, 32),
            Text = text,
            Anchor = anchor
        });

    private async Task PlayAsync()
    {
        StopPreview();
        if (!AnimationRecipeDesignValue.TryValidate(input.Text, out string? error))
        {
            status.Text = $"Preview unavailable. {error}";
            return;
        }

        using var effect = new AnimationRecipeEffect(input.Text);
        // The policy may scale durations or suspend ticks. A single deadline independently
        // cancels scheduler work and is disposed on stop, restart or dialog teardown.
        var previewDeadline = new CancellationTokenSource(PreviewDeadline);
        deadline = previewDeadline;
        AnimationRun started;
        try
        {
            started = effect.CreateDefinition(sample).Start(sample, cancellationToken: previewDeadline.Token);
        }
        catch (InvalidOperationException)
        {
            StopPreview();
            status.Text = "Preview unavailable. The animation scheduler is not available.";
            return;
        }
        run = started;
        status.Text = "Playing";
        var dispatcher = Dispatcher.UIThread;
        AnimationState result = await started.Completion.ConfigureAwait(false);
        if (dispatcher.CheckAccess())
            FinishPreview(started, result);
        else
            dispatcher.Post(() => FinishPreview(started, result));
    }

    private void FinishPreview(AnimationRun started, AnimationState result)
    {
        if (!ReferenceEquals(run, started))
            return;

        run = null;
        deadline?.Dispose();
        deadline = null;
        if (result != AnimationState.Completed)
            ResetSample();
        status.Text = result switch
        {
            AnimationState.Completed => "Preview complete.",
            AnimationState.Canceled => "Preview stopped at its time limit.",
            _ => "Preview unavailable. The animation could not complete."
        };
    }

    private void UpdateStatus()
        => status.Text = AnimationRecipeDesignValue.TryValidate(input.Text, out string? error)
            ? "Preview is ready. The sample changes only when you play it."
            : $"Preview unavailable. {error}";

    private void StopPreview()
    {
        AnimationRun? previous = run;
        run = null;
        previous?.Cancel();
        deadline?.Dispose();
        deadline = null;
        ResetSample();
    }

    private void ResetSample()
    {
        if (sample.IsDisposed) return;
        sample.Opacity = 1f;
        sample.ScaleX = 1f;
        sample.ScaleY = 1f;
        sample.Rotation = 0f;
    }

    /// <inheritdoc/>
    public override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel) StopPreview();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) StopPreview();
        base.Dispose(disposing);
    }
}
