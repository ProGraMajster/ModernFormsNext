using ModernFormsNext.Accessibility;
﻿using ModernFormsNext.Renderers;
using SkiaSharp;
using System.Drawing;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a label control that can display one or more clickable links.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="LinkLabel"/> extends <see cref="Label"/> by adding support for
    /// clickable text ranges, link states, keyboard navigation, and custom link colors.
    /// </para>
    /// <para>
    /// This control is rendered by <see cref="LinkLabelRenderer"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var label = new LinkLabel
    /// {
    ///     Text = "Visit documentation or support",
    ///     Width = 320,
    ///     Height = 30
    /// };
    ///
    /// label.Links.Clear();
    /// label.Links.Add(new LinkLabel.Link(0, 13) { Tag = "docs" });
    /// label.Links.Add(new LinkLabel.Link(17, 7) { Tag = "support" });
    ///
    /// label.LinkClicked += (sender, e) =>
    /// {
    ///     Console.WriteLine(e.Link?.Tag);
    /// };
    /// </code>
    /// </example>
    public partial class LinkLabel : Label
    {
        private Link? focus_link;
        private Link? hovered_link;
        private Link? pressed_link;

        private SKColor link_color = SKColor.Empty;
        private SKColor active_link_color = SKColor.Empty;
        private SKColor visited_link_color = SKColor.Empty;
        private SKColor disabled_link_color = SKColor.Empty;

        private LinkBehavior link_behavior = LinkBehavior.SystemDefault;
        private LinkCollection? link_collection;
        private bool layout_invalidated = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkLabel"/> class.
        /// </summary>
        public LinkLabel()
        {
            SetControlBehavior(ControlBehaviors.Hoverable | ControlBehaviors.InvalidateOnTextChanged);
            SetControlBehavior(ControlBehaviors.Selectable, true);

            TabStop = true;
            ResetLinkArea();
        }

        /// <summary>
        /// Gets the default <see cref="ControlStyle"/> for all <see cref="LinkLabel"/> instances.
        /// </summary>
        public new static readonly ControlStyle DefaultStyle = new ControlStyle(Label.DefaultStyle,
            style => {
                style.ForegroundColor = Theme.AccentColor;
            });

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle(DefaultStyle);

        /// <summary>
        /// Gets or sets the color used to display active links.
        /// </summary>
        public SKColor ActiveLinkColor
        {
            get => active_link_color == SKColor.Empty ? Theme.AccentColor2 : active_link_color;
            set
            {
                if (active_link_color != value)
                {
                    active_link_color = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the color used to display disabled links.
        /// </summary>
        public SKColor DisabledLinkColor
        {
            get => disabled_link_color == SKColor.Empty ? Theme.ForegroundDisabledColor : disabled_link_color;
            set
            {
                if (disabled_link_color != value)
                {
                    disabled_link_color = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the range in the text treated as the primary link.
        /// </summary>
        /// <remarks>
        /// Setting this property clears the current <see cref="Links"/> collection
        /// and replaces it with a single link.
        /// </remarks>
        public LinkArea LinkArea
        {
            get
            {
                if (Links.Count == 0)
                    return new LinkArea(0, 0);

                return new LinkArea(Links[0].Start, Links[0].RawLength);
            }
            set
            {
                if (value.Start < 0)
                    throw new ArgumentOutOfRangeException(nameof(LinkArea), "Start must be greater than or equal to zero.");

                if (value.Length < -1)
                    throw new ArgumentOutOfRangeException(nameof(LinkArea), "Length must be greater than or equal to -1.");

                Links.Clear();

                if (!(value.Start == 0 && value.Length == 0))
                {
                    Links.Add(new Link(value.Start, value.Length));
                }

                UpdateSelectability();
                InvalidateLayout();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets how link text should be underlined.
        /// </summary>
        public LinkBehavior LinkBehavior
        {
            get => link_behavior;
            set
            {
                if (link_behavior != value)
                {
                    link_behavior = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the color used to display normal links.
        /// </summary>
        public SKColor LinkColor
        {
            get => link_color == SKColor.Empty ? Theme.AccentColor : link_color;
            set
            {
                if (link_color != value)
                {
                    link_color = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets the collection of links contained in this control.
        /// </summary>
        public LinkCollection Links => link_collection ??= new LinkCollection(this);

        /// <summary>
        /// Gets or sets a value indicating whether the primary link has been visited.
        /// </summary>
        public bool LinkVisited
        {
            get => Links.Count > 0 && Links[0].Visited;
            set
            {
                if (Links.Count == 0)
                    Links.Add(new Link(0, -1));

                if (Links[0].Visited != value)
                {
                    Links[0].Visited = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the color used to display visited links.
        /// </summary>
        public SKColor VisitedLinkColor
        {
            get => visited_link_color == SKColor.Empty ? SKColors.Purple : visited_link_color;
            set
            {
                if (visited_link_color != value)
                {
                    visited_link_color = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Occurs when a link is clicked.
        /// </summary>
        public event EventHandler<LinkLabelLinkClickedEventArgs>? LinkClicked;

        /// <summary>
        /// Gets the link currently focused for keyboard interaction.
        /// </summary>
        internal Link? FocusLink
        {
            get => focus_link;
            set
            {
                if (!ReferenceEquals(focus_link, value))
                {
                    focus_link = value;
                    Invalidate();
                    NotifyAccessibilityLinkChanged(value, Accessibility.AccessibleEvents.Focus);
                }
            }
        }

        private SKColor hover_link_color = SKColor.Empty;

        /// <summary>
        /// Gets or sets the color used to display hovered links.
        /// </summary>
        public SKColor HoverLinkColor
        {
            get => hover_link_color == SKColor.Empty ? Theme.AccentColor2 : hover_link_color;
            set
            {
                if (hover_link_color != value)
                {
                    hover_link_color = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets the link currently hovered by the pointer.
        /// </summary>
        internal Link? HoveredLink => hovered_link;

        /// <summary>
        /// Gets the link currently pressed by the pointer.
        /// </summary>
        internal Link? PressedLink => pressed_link;

        /// <summary>
        /// Gets a value indicating whether cached link layout must be recalculated.
        /// </summary>
        internal bool IsLayoutInvalidated => layout_invalidated;

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Links.ClearVisualBounds();

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);

            foreach (var link in Links)
                link.State &= ~(LinkState.Hover | LinkState.Active);

            hovered_link = null;
            pressed_link = null;

            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            var lifetime = new Accessibility.AccessibilityControlLifetime(this);
            base.OnKeyDown(e);
            if (!IsCurrentLinkInput(lifetime) || e.Handled) return;

            switch (e.KeyCode)
            {
                case Keys.Enter:
                case Keys.Space:
                    if (FocusLink is not null && FocusLink.Enabled)
                    {
                        ActivateLink(FocusLink, MouseButtons.Left);
                        e.Handled = true;
                    }

                    break;

                case Keys.Left:
                case Keys.Up:
                    if (FocusNextLink(false))
                        e.Handled = true;

                    break;

                case Keys.Right:
                case Keys.Down:
                    if (FocusNextLink(true))
                        e.Handled = true;

                    break;
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            var lifetime = new Accessibility.AccessibilityControlLifetime(this);
            base.OnMouseDown(e);
            if (!IsCurrentLinkInput(lifetime) || e.Handled) return;

            if (!Enabled || !e.Button.HasFlag(MouseButtons.Left))
                return;

            Select();
            if (!IsCurrentLinkInput(lifetime)) return;

            var link = PointInLink(e.Location);
            if (link is null || !link.Enabled)
                return;

            pressed_link = link;
            link.State |= LinkState.Active;
            FocusLink = link;
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            var invalidate = false;

            if (hovered_link is not null)
            {
                hovered_link.State &= ~LinkState.Hover;
                hovered_link = null;
                invalidate = true;
            }

            if (pressed_link is not null)
            {
                pressed_link.State &= ~LinkState.Active;
                pressed_link = null;
                invalidate = true;
            }

            if (invalidate)
                Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var lifetime = new Accessibility.AccessibilityControlLifetime(this);
            base.OnMouseMove(e);
            if (!IsCurrentLinkInput(lifetime) || e.Handled) return;

            if (!Enabled)
                return;

            var link = PointInLink(e.Location);

            if (!ReferenceEquals(hovered_link, link))
            {
                if (hovered_link is not null)
                    hovered_link.State &= ~LinkState.Hover;

                hovered_link = link;

                if (hovered_link is not null && hovered_link.Enabled)
                    hovered_link.State |= LinkState.Hover;

                Invalidate();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            var lifetime = new Accessibility.AccessibilityControlLifetime(this);
            var pressed = pressed_link;
            // Retire the old press even when a public MouseUp observer throws or reparents.
            pressed_link = null;
            if (pressed is not null) pressed.State &= ~LinkState.Active;
            base.OnMouseUp(e);
            if (!IsCurrentLinkInput(lifetime) || e.Handled || !e.Button.HasFlag(MouseButtons.Left)) return;
            var released = PointInLink(e.Location);
            if (pressed is not null && ReferenceEquals(pressed, released)) ActivateLink(pressed, e.Button);
            if (IsCurrentLinkInput(lifetime)) Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            InvalidateLayout();
            Invalidate();
        }

        /// <inheritdoc/>
        internal override void OnPresentationContentMetricsChanged()
        {
            InvalidateLayout();
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            RenderManager.Render(this, e);
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            InvalidateLayout();
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnTextAlignChanged(EventArgs e)
        {
            base.OnTextAlignChanged(e);
            InvalidateLayout();
            Invalidate();
        }

        /// <inheritdoc/>
        protected override void OnTextChanged(EventArgs e)
        {
            NormalizeLinks();
            InvalidateLayout();
            UpdateSelectability();
            Exception? failure = null;
            try { base.OnTextChanged(e); }
            catch (Exception exception) { failure = exception; }
            try { OnLinkMetadataChanged(null, AccessibleEvents.Reorder, layout: true); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
            if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        /// <inheritdoc/>
        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            InvalidateLayout();
            base.SetBoundsCore(x, y, width, height, specified);
        }

        /// <summary>
        /// Raises the <see cref="LinkClicked"/> event.
        /// </summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnLinkClicked(LinkLabelLinkClickedEventArgs e)
            => LinkClicked?.Invoke(this, e);

        /// <summary>
        /// Determines whether the specified link should be underlined.
        /// </summary>
        /// <param name="link">The link to evaluate.</param>
        /// <returns><see langword="true"/> if the link should be underlined; otherwise, <see langword="false"/>.</returns>
        internal bool ShouldUnderline(Link link)
        {
            var behavior = LinkBehavior == LinkBehavior.SystemDefault
                ? LinkBehavior.AlwaysUnderline
                : LinkBehavior;

            return behavior switch
            {
                LinkBehavior.AlwaysUnderline => true,
                LinkBehavior.HoverUnderline => (link.State & LinkState.Hover) == LinkState.Hover,
                LinkBehavior.NeverUnderline => false,
                _ => true
            };
        }

        /// <summary>
        /// Resolves the effective display color for the specified link.
        /// </summary>
        /// <param name="link">The link whose color should be resolved.</param>
        /// <returns>The color that should be used when drawing the link.</returns>
        internal SKColor ResolveLinkColor(Link link)
        {
            if (!Enabled || !link.Enabled)
                return DisabledLinkColor;

            if ((link.State & LinkState.Active) == LinkState.Active)
                return ActiveLinkColor;

            if ((link.State & LinkState.Visited) == LinkState.Visited)
                return VisitedLinkColor;

            if ((link.State & LinkState.Hover) == LinkState.Hover)
                return HoverLinkColor;

            return LinkColor;
        }

        /// <summary>
        /// Marks the internal layout cache as invalid.
        /// </summary>
        internal void InvalidateLayout()
        {
            layout_invalidated = true;
            Links.ClearVisualBounds();
        }

        /// <summary>
        /// Marks the internal layout cache as valid.
        /// </summary>
        internal void ValidateLayout()
        {
            layout_invalidated = false;
        }

        /// <summary>
        /// Validates that no two links overlap.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when link ranges overlap.
        /// </exception>
        internal void ValidateNoOverlappingLinks()
        {
            for (var i = 0; i < Links.Count; i++)
            {
                var left = Links[i];
                var left_end = (long)left.Start + left.Length;

                for (var j = i + 1; j < Links.Count; j++)
                {
                    var right = Links[j];
                    var right_end = (long)right.Start + right.Length;

                    var max_start = Math.Max(left.Start, right.Start);
                    var min_end = Math.Min(left_end, right_end);

                    if (max_start < min_end)
                        throw new InvalidOperationException("Link ranges must not overlap.");
                }
            }
        }

        private bool ActivateLink(Link link, MouseButtons button)
        {
            if (activatingLink || !IsAccessibilityLinkAttached(link) || !Enabled || !Visible || !link.Enabled)
                return false;
            activatingLink = true;
            try
            {
                var lifetime = new Accessibility.AccessibilityControlLifetime(this);
                var parent = Parent;
                var window = FindWindow();
                link.Visited = true;
                if (!lifetime.IsCurrent || !IsCurrentAccessibilityLink(link, parent, window)) return false;
                FocusLink = link;
                if (!lifetime.IsCurrent || !IsCurrentAccessibilityLink(link, parent, window)) return false;

                OnLinkClicked(new LinkLabelLinkClickedEventArgs(link, button));
                if (IsCurrentAccessibilityLink(link, parent, window)) Invalidate();
                return true;
            }
            finally { activatingLink = false; }
        }

        private bool FocusNextLink(bool forward)
        {
            if (Links.Count == 0)
                return false;

            var current_index = FocusLink is null
                ? (forward ? -1 : Links.Count)
                : Links.IndexOf(FocusLink);

            var next_index = GetNextLinkIndex(current_index, forward);

            if (next_index < 0)
            {
                next_index = forward
                    ? GetNextLinkIndex(-1, true)
                    : GetNextLinkIndex(Links.Count, false);
            }

            if (next_index < 0)
                return false;

            FocusLink = Links[next_index];
            return true;
        }

        private int GetNextLinkIndex(int index, bool forward)
        {
            if (forward)
            {
                for (var i = index + 1; i < Links.Count; i++)
                {
                    if (Links[i].Enabled && Links[i].Length > 0)
                        return i;
                }
            }
            else
            {
                for (var i = index - 1; i >= 0; i--)
                {
                    if (Links[i].Enabled && Links[i].Length > 0)
                        return i;
                }
            }

            return -1;
        }

        private void NormalizeLinks()
        {
            if (Links.Count == 0)
                return;

            var text_length = Text?.Length ?? 0;

            // Normalize all ranges without callbacks or sorting a live enumerator. The
            // completed text/range state is published together by OnTextChanged.
            foreach (var link in Links)
            {
                int start = Math.Clamp(link.Start, 0, text_length);
                int length = link.RawLength == -1 ? -1 : Math.Clamp(link.RawLength, 0, text_length - start);
                link.NormalizeRange(start, length);
            }
            Links.SortByStart();

            ValidateNoOverlappingLinks();
        }

        private Link? PointInLink(Point location)
        {
            var renderer = RenderManager.GetRenderer<LinkLabelRenderer>()
                ?? throw new InvalidOperationException("No LinkLabelRenderer has been registered.");

            return renderer.HitTest(this, location);
        }

        private void ResetLinkArea()
        {
            Links.Clear();
            Links.Add(new Link(0, -1));
            UpdateSelectability();
        }

        private void UpdateSelectability()
        {
            var selectable = Links.Any(link => link.Enabled && IsAccessibilityLinkAttached(link));

            TabStop = selectable;
            SetControlBehavior(ControlBehaviors.Selectable, selectable);

            if (!selectable || (FocusLink is { } focused && (!IsAccessibilityLinkAttached(focused) || !focused.Enabled)))
                FocusLink = null;
        }
    }

    internal static class LinkLabelColorExtensions
    {
        internal static Color ToColor(this SkiaSharp.SKColor color)
            => Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }
}
