using System;
using System.Collections.Generic;
using System.Drawing;
using ModernFormsNext;
using ModernFormsNext.Accessibility;

namespace ControlGallery.Panels;

/// <summary>
/// Provides a focused development surface for manual accessibility and UI Automation validation.
/// </summary>
public sealed class AccessibilityPanel : Panel
{
    private readonly ListBox dynamicList;
    private readonly Button dynamicTarget;
    private readonly Button hiddenTarget;
    private readonly Label status;
    private int addedItemNumber;

    /// <summary>
    /// Initializes the accessibility validation controls and their stable automation identifiers.
    /// </summary>
    public AccessibilityPanel()
    {
        Controls.Add(new Label
        {
            Left = 20,
            Top = 20,
            Width = 300,
            Text = "Dynamic logical items"
        });

        dynamicList = Controls.Add(new ListBox
        {
            Left = 20,
            Top = 50,
            Width = 240,
            Height = 170,
            SelectionMode = SelectionMode.One,
            AccessibleName = "Dynamic accessibility test list",
            AccessibleAutomationId = "controlgallery.accessibility.dynamic-list"
        });
        dynamicList.Items.AddRange("Alpha", "Bravo", "Charlie");

        AddActionButton(290, 50, "Add item", "controlgallery.accessibility.add", AddItem);
        AddActionButton(290, 90, "Remove item", "controlgallery.accessibility.remove", RemoveItem);
        AddActionButton(290, 130, "Move last first", "controlgallery.accessibility.reorder", ReorderItems);

        dynamicTarget = Controls.Add(new Button
        {
            Left = 20,
            Top = 255,
            Width = 180,
            Text = "Dynamic target",
            AccessibleName = "Dynamic target",
            AccessibleAutomationId = "controlgallery.accessibility.dynamic-target"
        });

        AddActionButton(220, 255, "Rename target", "controlgallery.accessibility.rename", RenameTarget);
        AddActionButton(390, 255, "Toggle enabled", "controlgallery.accessibility.enabled", ToggleTargetEnabled);
        AddActionButton(560, 255, "Toggle visible", "controlgallery.accessibility.visible", ToggleTargetVisible);

        hiddenTarget = Controls.Add(new Button
        {
            Left = 20,
            Top = 300,
            Width = 180,
            Text = "Hidden target",
            Visible = false,
            AccessibleName = "Hidden target",
            AccessibleAutomationId = "controlgallery.accessibility.hidden-target"
        });

        AddActionButton(220, 300, "Show hidden", "controlgallery.accessibility.show-hidden", ToggleHiddenTarget);

        Controls.Add(new Label
        {
            Left = 20,
            Top = 365,
            Width = 430,
            Text = "Custom semantic surface (one child without a Control instance)"
        });

        Controls.Add(new CustomSemanticSurface(OnCustomSemanticAction)
        {
            Left = 20,
            Top = 395,
            Width = 430,
            Height = 70,
            AccessibleName = "Custom semantic surface",
            AccessibleAutomationId = "controlgallery.accessibility.custom-surface"
        });

        status = Controls.Add(new Label
        {
            Left = 20,
            Top = 485,
            Width = 600,
            Text = "Ready",
            AccessibleAutomationId = "controlgallery.accessibility.status"
        });
    }

    private void AddActionButton(int left, int top, string text, string automationId, Action action)
    {
        Button button = Controls.Add(new Button
        {
            Left = left,
            Top = top,
            Width = 150,
            Text = text,
            AccessibleAutomationId = automationId
        });
        button.Click += (_, _) => action();
    }

    private void AddItem()
    {
        addedItemNumber++;
        dynamicList.Items.Add($"Added {addedItemNumber}");
        status.Text = "Item added";
    }

    private void RemoveItem()
    {
        if (dynamicList.Items.Count == 0)
            return;

        dynamicList.Items.RemoveAt(dynamicList.Items.Count - 1);
        status.Text = "Item removed";
    }

    private void ReorderItems()
    {
        if (dynamicList.Items.Count < 2)
            return;

        dynamicList.Items.Move(dynamicList.Items.Count - 1, 0);
        status.Text = "Items reordered";
    }

    private void RenameTarget()
    {
        dynamicTarget.AccessibleName = dynamicTarget.AccessibleName == "Dynamic target"
            ? "Dynamic target renamed"
            : "Dynamic target";
        status.Text = "Accessible name changed";
    }

    private void ToggleTargetEnabled()
    {
        dynamicTarget.Enabled = !dynamicTarget.Enabled;
        status.Text = "Enabled state changed";
    }

    private void ToggleTargetVisible()
    {
        dynamicTarget.Visible = !dynamicTarget.Visible;
        status.Text = "Visible state changed";
    }

    private void ToggleHiddenTarget()
    {
        hiddenTarget.Visible = !hiddenTarget.Visible;
        status.Text = "Hidden target visibility changed";
    }

    private void OnCustomSemanticAction()
        => status.Text = "Custom semantic child invoked";

    private sealed class CustomSemanticSurface : Control
    {
        private readonly Action invoke;

        public CustomSemanticSurface(Action invoke)
        {
            this.invoke = invoke;
            Style.Border.Width = 1;
            Style.Border.Color = Theme.BorderMidColor;
            Style.BackgroundColor = Theme.ControlLowColor;
        }

        protected override AccessibleObject CreateAccessibilityInstance()
            => new CustomSemanticSurfaceAccessibleObject(this, invoke);
    }

    private sealed class CustomSemanticSurfaceAccessibleObject : Control.ControlAccessibleObject
    {
        private readonly CustomSemanticChild child;

        public CustomSemanticSurfaceAccessibleObject(CustomSemanticSurface owner, Action invoke)
            : base(owner)
        {
            child = new CustomSemanticChild(this, invoke);
        }

        public override AccessibleControlType ControlType => AccessibleControlType.Group;

        public override AccessibleRole Role => AccessibleRole.Grouping;

        protected override IEnumerable<AccessibleObject> GetAccessibilityChildren()
        {
            // This node deliberately has no corresponding Control. It validates that platform
            // providers consume the canonical logical tree rather than reconstructing controls.
            yield return child;
        }
    }

    private sealed class CustomSemanticChild : AccessibleObject
    {
        private readonly WeakReference<AccessibleObject> parent;
        private readonly Action invoke;

        public CustomSemanticChild(AccessibleObject parent, Action invoke)
        {
            this.parent = new WeakReference<AccessibleObject>(parent);
            this.invoke = invoke;
            AutomationId = "controlgallery.accessibility.custom-action";
        }

        public override Rectangle Bounds
        {
            get
            {
                Rectangle parentBounds = Parent?.Bounds ?? Rectangle.Empty;
                return new Rectangle(
                    parentBounds.Left + 12,
                    parentBounds.Top + 12,
                    Math.Max(0, parentBounds.Width - 24),
                    Math.Max(0, parentBounds.Height - 24));
            }
        }

        public override AccessibleControlType ControlType => AccessibleControlType.Button;

        public override string? Name
        {
            get => "Invoke custom semantic action";
            set { }
        }

        public override AccessibleObject? Parent
            => parent.TryGetTarget(out AccessibleObject? value) ? value : null;

        public override AccessibleRole Role => AccessibleRole.PushButton;

        public override AccessibleActions SupportedActions => AccessibleActions.Invoke;

        public override bool PerformAction(AccessibleActions action, object? parameter = null)
        {
            if (action != AccessibleActions.Invoke || parameter is not null)
                return false;

            invoke();
            return true;
        }
    }
}
