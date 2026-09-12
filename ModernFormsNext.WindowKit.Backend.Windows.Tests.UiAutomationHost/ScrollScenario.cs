using System.Drawing;
using System.Text.Json;
using ModernFormsNext;
using ModernFormsNext.Accessibility;
using ModernFormsNext.WindowKit.Threading;

internal static class ScrollScenario
{
    internal static void Run()
    {
        using var form = new Form { ClientSize = new(420, 260), Text = "ModernFormsNext native Scroll integration" };
        var observation = form.Controls.Add(new Label { AccessibleAutomationId = "uia.scroll.focus-observation",
            Text = "Canonical scroll observation", Bounds = new(230, 120, 170, 50) });
        int scrollActions = 0;
        bool focusPreserved = true;
        void ObserveFocus(long? before, long? after)
        {
            scrollActions++;
            focusPreserved &= before.HasValue && before == after;
            observation.AccessibleName = JsonSerializer.Serialize(new { Count = scrollActions, Preserved = focusPreserved });
        }
        var viewport = form.Controls.Add(new FocusObservingViewport(ObserveFocus) {
            AccessibleAutomationId = "uia.scroll.viewport", AccessibleName = "Scrollable content",
            Bounds = new(20, 20, 200, 120), AutoScroll = true });
        viewport.Controls.Add(new Control { Bounds = new(0, 0, 600, 500) });
        viewport.Controls.Add(new Button { AccessibleAutomationId = "uia.scroll.target", Text = "Reveal target",
            Bounds = new(540, 430, 50, 30) });
        var focus = form.Controls.Add(new Button { AccessibleAutomationId = "uia.scroll.focus", Text = "Retain focus",
            Bounds = new(230, 20, 150, 35) });
        var disable = form.Controls.Add(new Button { AccessibleAutomationId = "uia.scroll.disable", Text = "Disable viewport",
            Bounds = new(230, 65, 150, 35) });
        disable.Click += (_, _) => viewport.Enabled = false;
        // Shown still runs inside startup's Show transaction. Publish readiness only after
        // its initial focus selection has unwound and the actual dispatcher loop is running.
        form.Shown += (_, _) => Dispatcher.UIThread.Post(() => {
            focus.Select();
            Console.WriteLine($"HWND:{form.PlatformHandle.Handle.ToInt64()}");
        });
        Application.Run(form);
    }

    private sealed class FocusObservingViewport(Action<long?, long?> observed) : ScrollableControl
    {
        /// <inheritdoc/>
        protected override AccessibleObject CreateAccessibilityInstance() => new FocusObservingPeer(this, observed);
    }

    private sealed class FocusObservingPeer(Control owner, Action<long?, long?> observed) : Control.ControlAccessibleObject(owner)
    {
        /// <inheritdoc/>
        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action != AccessibleActions.Scroll) return base.PerformAction(action, parameter);
            // UIAutomationCore may issue a distinct SetFocus before the pattern action. Observe
            // the real canonical action itself; never restore/replace the client's focus choice.
            AccessibleObject root = this;
            for (int depth = 0; depth < 512 && root.Parent is { } parent; depth++) root = parent;
            long? before = root.GetFocused()?.RuntimeId;
            bool result = base.PerformAction(action, parameter);
            observed(before, root.GetFocused()?.RuntimeId);
            return result;
        }
    }
}
