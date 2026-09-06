using System.Collections.Specialized;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Input;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Layout;
using ModernFormsNext.Renderers;
using SkiaSharp;

namespace ModernFormsNext
{
    /// <summary>
    /// Represents a Button control.
    /// </summary>
    public class Button : Control, IHaveTextAndImageAlign, ICommandBindingTargetProvider
    {
        private static readonly BitVector32.Section s_stateAutoEllipsis = BitVector32.CreateSection (1);

        private static readonly int s_propImage = PropertyStore.CreateKey ();
        private static readonly int s_propImageAlign = PropertyStore.CreateKey ();
        private static readonly int s_propImageList = PropertyStore.CreateKey ();
        private static readonly int s_propImageIndex = PropertyStore.CreateKey ();
        private static readonly int s_propImageKey = PropertyStore.CreateKey ();
        private static readonly int s_propTextAlign = PropertyStore.CreateKey ();
        private static readonly int s_propTextImageRelation = PropertyStore.CreateKey ();

        private BitVector32 _buttonState;
        private CommandSource? commandSource;

        /// <summary>
        /// Gets or sets the command executed after <see cref="Control.Click"/> on activation.
        /// </summary>
        /// <remarks>
        /// Assign on the UI thread. Null keeps event-only behavior. Availability contributes to
        /// effective <see cref="Control.Enabled"/> without changing local enabled intent.
        /// Replacement/removal detaches the previous command; disposal detaches the current one.
        /// CanExecute exceptions disable this source and propagate unchanged; a later notification
        /// or assignment can recover. Background notifications use the existing UI dispatcher.
        /// The source does not own or dispose the command. Designer serialization is deferred.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ICommand? Command {
            get => commandSource?.Command;
            set {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                (commandSource ??= new CommandSource(this)).Command = value;
            }
        }

        /// <summary>
        /// Gets or sets the parameter used to evaluate and execute <see cref="Command"/>.
        /// </summary>
        /// <remarks>
        /// Null is supported. Assign on the UI thread; a different reference immediately
        /// reevaluates CanExecute and may update enabled/rendering/accessibility state. For
        /// mutation within the same object, raise the command's CanExecuteChanged event.
        /// The current parameter is used after Click and is released on disposal, without being
        /// disposed. Exceptions follow the same policy as <see cref="Command"/>.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? CommandParameter {
            get => commandSource?.Parameter;
            set {
                ObjectDisposedException.ThrowIf(IsDisposed, this);
                (commandSource ??= new CommandSource(this)).Parameter = value;
            }
        }

        bool ICommandBindingTargetProvider.IsCommandSourceDisposed => IsDisposed;
        void ICommandBindingTargetProvider.SetCommandEnabled(bool enabled) => SetCommandEnabled(enabled);

        /// <summary>
        /// Initializes a new instance of the Button class.
        /// </summary>
        public Button ()
        {
            SetControlBehavior (ControlBehaviors.Hoverable);
            SetControlBehavior (ControlBehaviors.InvalidateOnTextChanged);
        }

        /// <summary>
        /// Gets or sets a value indicating if text will be truncated with an ellipsis if it cannot fully fit in the <see cref='Button'/>.
        /// </summary>
        public bool AutoEllipsis {
            get => _buttonState[s_stateAutoEllipsis] != 0;
            set {
                if (AutoEllipsis != value) {

                    _buttonState[s_stateAutoEllipsis] = value ? 1 : 0;

                    if (Parent is not null)
                        LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.AutoEllipsis);

                    Invalidate ();
                }
            }
        }

        /// <summary>
        ///  Allows the control to optionally shrink when <see cref="Control.AutoSize"/> is <see langword="true"/>.
        /// </summary>
        public AutoSizeMode AutoSizeMode {
            get => GetAutoSizeMode ();
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (GetAutoSizeMode () != value) {
                    SetAutoSizeMode (value);
                    if (Parent is not null) {
                        // DefaultLayout does not keep anchor information until it needs to. When
                        // AutoSize became a common property, we could no longer blindly call into
                        // DefaultLayout, so now we do a special InitLayout just for DefaultLayout.
                        if (Parent.LayoutEngine == DefaultLayout.Instance)
                            Parent.LayoutEngine.InitLayout (this, BoundsSpecified.Size);

                        LayoutTransaction.DoLayout (Parent, this, PropertyNames.AutoSize);
                    }
                }
            }
        }

        /// <inheritdoc/>
        protected override Cursor DefaultCursor => Cursors.Hand;

        /// <inheritdoc/>
        protected override Size DefaultSize => new Size (100, 30);

        /// <summary>
        /// The default ControlStyle for all instances of Button.
        /// </summary>
        public new static ControlStyle DefaultStyle = new ControlStyle (Control.DefaultStyle,
            (style) => style.Border.Width = 1);

        /// <summary>
        /// The default hover ControlStyle for all instances of Button.
        /// </summary>
        public new static ControlStyle DefaultStyleHover = new ControlStyle (DefaultStyle,
            (style) => {
                style.BackgroundColor = Theme.AccentColor;
                style.Border.Color = Theme.AccentColor2;
                style.ForegroundColor = Theme.ForegroundColorOnAccent;
            });

        /// <summary>
        /// Gets or sets a value that is returned to the parent form when the button is clicked.
        /// </summary>
        public DialogResult DialogResult { get; set; }

        /// <summary>
        /// Gets or sets the image displayed on the <see cref='Button'/>.
        /// </summary>
        public SKBitmap? Image {
            get => Properties.GetObject<SKBitmap> (s_propImage);
            set {
                if (Image != value) {
                    Properties.SetObject (s_propImage, value);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the alignment of the image on the <see cref='Button'/>.
        /// </summary>
        public ContentAlignment ImageAlign {
            get => Properties.GetEnum (s_propImageAlign, ContentAlignment.MiddleLeft);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != ImageAlign) {
                    Properties.SetEnum (s_propImageAlign, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.ImageAlign);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the image in the <see cref='ImageList'/> to display on the <see cref='Button'/>.
        /// </summary>
        public int ImageIndex {
            get => Properties.GetInteger (s_propImageIndex, -1);
            set {
                if (ImageIndex != value) {
                    Properties.SetInteger (s_propImageIndex, value);

                    // Setting this clears any existing ImageKey and Image
                    if (value >= 0) {
                        Properties.RemoveObject (s_propImage);
                        Properties.RemoveObject (s_propImageKey);
                    }

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the key of the image in the <see cref='ImageList'/> to display on the <see cref='Button'/>.
        /// </summary>
        public string ImageKey {
            get => Properties.GetObject<string> (s_propImageKey) ?? string.Empty;
            set {
                if (ImageKey != value) {
                    Properties.SetObject (s_propImageKey, value);

                    // Setting this clears any existing ImageIndex and Image
                    if (value is not null) {
                        Properties.RemoveObject (s_propImage);
                        Properties.RemoveInteger (s_propImageIndex);
                    }

                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref='ImageList'/> that contains the image to display on the <see cref='Button'/>.
        /// </summary>
        public ImageList? ImageList {
            get => Properties.GetObject<ImageList> (s_propImageList);
            set {
                if (ImageList != value) {
                    Properties.SetObject (s_propImageList, value);

                    // If an image list is set, clear any existing image
                    if (value is not null)
                        Properties.RemoveObject (s_propImage);

                    Invalidate ();
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnClick (MouseEventArgs e)
        {
            if (IsDisposed || !Enabled)
                return;
            if (e.Button == MouseButtons.Left && commandSource is not null && !commandSource.CanExecute())
                return;

            if (FindForm () is Form form)
                form.DialogResult = DialogResult;

            base.OnClick (e);

            if (e.Button == MouseButtons.Left && !IsDisposed && Enabled)
                commandSource?.Execute();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            commandSource?.Dispose();
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.KeyCode.In (Keys.Space, Keys.Enter)) {
                NotifyInteractionKeyUp (e);
                PerformClick ();
                e.Handled = true;
                return;
            }

            base.OnKeyUp (e);
        }

        /// <inheritdoc/>
        protected override void OnPaint (PaintEventArgs e)
        {
            base.OnPaint (e);

            RenderManager.Render (this, e);
        }

        internal override void RenderFocusOverlay (PaintEventArgs e)
        {
            if (Selected && ShowFocusCues)
                e.Canvas.DrawFocusRectangle (TextImageLayoutEngine.Layout (this).Focus, 0);
        }

        /// <summary>
        /// Activates the button through its normal Click and command path.
        /// </summary>
        /// <remarks>
        /// Call on the UI thread. Disabled or disposed buttons do nothing. When allowed, applies
        /// DialogResult, raises Click, then reevaluates and executes the current command with the
        /// current parameter. A Click exception prevents command execution; predicate and execute
        /// exceptions propagate unchanged. CanExecute is checked before Click as well, so a stale
        /// enabled snapshot cannot invoke an unavailable command. Visibility is not required for
        /// programmatic activation. Keyboard and accessibility activation use this same path.
        /// </remarks>
        public void PerformClick ()
        {
            OnClick (new MouseEventArgs (MouseButtons.Left, 1, 0, 0, Point.Empty));
        }

        /// <inheritdoc/>
        public override ControlStyle Style { get; } = new ControlStyle (DefaultStyle);

        /// <inheritdoc/>
        public override ControlStyle StyleHover { get; } = new ControlStyle (DefaultStyleHover);

        /// <summary>
        /// Gets or sets the alignment of the text on the <see cref='Button'/>.
        /// </summary>
        public ContentAlignment TextAlign {
            get => Properties.GetEnum (s_propTextAlign, ContentAlignment.MiddleLeft);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != TextAlign) {
                    Properties.SetEnum (s_propTextAlign, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.TextAlign);
                    Invalidate ();
                }
            }
        }

        /// <summary>
        /// Gets or sets the alignment of the text relative to the image on the <see cref='Button'/>.
        /// </summary>
        public TextImageRelation TextImageRelation {
            get => Properties.GetEnum (s_propTextImageRelation, TextImageRelation.ImageBeforeText);
            set {
                SourceGenerated.EnumValidator.Validate (value);

                if (value != TextImageRelation) {
                    Properties.SetEnum (s_propTextImageRelation, value);
                    LayoutTransaction.DoLayoutIf (AutoSize, Parent, this, PropertyNames.TextImageRelation);
                    Invalidate ();
                }
            }
        }

        bool IHaveTextAndImageAlign.Multiline => false;

        /// <inheritdoc/>
        public override string ToString () => $"{base.ToString ()}, Text: {Text}";
    }
}
