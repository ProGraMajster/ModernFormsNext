// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace ModernFormsNext.Layout;

internal partial class FlowLayout
{
    /// <summary>
    ///  The goal of the FlowLayout Engine is to always layout from left to right. In order to achieve different
    ///  flow directions we have "Proxies" for the Container (the thing laying out) and for setting the bounds of the
    ///  child elements.
    ///
    ///  We have a base ContainerProxy, and derived proxies for all of the flow directions. In order to achieve flow direction of RightToLeft,
    ///  the RightToLeft container proxy detects when we're going to set the bounds and translates it to the right.
    ///
    ///  In order to do a vertical flow, such as TopDown, we pretend we're laying out horizontally. The main way this is
    ///  achieved is through the use of the VerticalElementProxy, which flips all rectangles and sizes.
    ///
    ///  In order to do BottomUp, we combine the same techniques of TopDown with the RightToLeft flow. That is,
    ///  we override the bounds, and translate from left to right, AND use the VerticalElementProxy.
    ///
    ///  A final note: This layout engine does all its RightToLeft translation itself. It does not support
    ///  WS_EX_LAYOUTRTL (OS mirroring).
    /// </summary>
    private class ContainerProxy
    {
        private readonly IArrangedElement _container;
        private readonly bool _isContainerRTL;
        private Rectangle _displayRect;
        private Rectangle _reflectionRect;
        private ElementProxy? _elementProxy;

        public ContainerProxy (IArrangedElement container)
        {
            _container = container;
            _isContainerRTL = container is Control { RightToLeft: RightToLeft.Yes };
        }

        public virtual Rectangle Bounds {
            set {
                if (IsContainerRTL) {

                    // Offset the X coordinate.
                    if (IsVertical)
                        // Offset the Y value here, since it is really the X value.
                        value.Y = _reflectionRect.Top + _reflectionRect.Bottom - value.Bottom;
                    else
                        value.X = _reflectionRect.Left + _reflectionRect.Right - value.Right;
                }

                ElementProxy.Bounds = value;
            }
        }

        public IArrangedElement Container => _container;

        /// <summary>
        ///  Returns the display rectangle of the container - this will be flipped if the layout
        ///  is a vertical layout.
        /// </summary>
        public Rectangle DisplayRect {
            get => _displayRect;
            set {
                if (_displayRect != value) {
                    // flip the displayRect since when we do layout direction TopDown/BottomUp, we layout the controls
                    // on the flipped rectangle as if our layout direction were LeftToRight/RightToLeft. In this case
                    // we can save some code bloat
                    _displayRect = LayoutUtils.FlipRectangleIf (IsVertical, value);
                    _reflectionRect = _displayRect;
                }
            }
        }

        // Wrapping is constrained by DisplayRect, but an overflowing reverse layout must
        // reflect across its whole content extent. Otherwise children land at negative,
        // unreachable coordinates while the canonical scrollbar can only move positively.
        public Size ReflectionSize {
            set {
                Size extent = LayoutUtils.FlipSizeIf(IsVertical, value);
                _reflectionRect = DisplayRect;
                _reflectionRect.Width = Math.Max(_reflectionRect.Width, extent.Width);
                _reflectionRect.Height = Math.Max(_reflectionRect.Height, extent.Height);
            }
        }

        /// <summary>
        ///  Returns the element proxy to use. A vertical element proxy will typically flip
        ///  all the sizes and rectangles so that it can fake being laid out in a
        ///  horizontal manner.
        /// </summary>
        public ElementProxy ElementProxy {
            get {
                if (_elementProxy is null)
                    _elementProxy = IsVertical ? new VerticalElementProxy () : new ElementProxy ();

                return _elementProxy;
            }
        }

        protected bool IsContainerRTL => _isContainerRTL;

        /// <summary>
        ///  Specifies if we're TopDown or BottomUp and should use the VerticalElementProxy to
        ///  translate
        /// </summary>
        protected virtual bool IsVertical => false;

        /// <summary>
        ///  Used when you want to translate from right to left, but preserve Margin.Right
        ///  and Margin.Left.
        /// </summary>
        protected Rectangle RTLTranslateNoMarginSwap (Rectangle bounds)
        {
            var newBounds = bounds;
            // Reflect around the complete padded, scrolled rectangle. Its origin already
            // contains the canonical scroll offset; adding another translation would drift.
            newBounds.X = _reflectionRect.Left + _reflectionRect.Right - bounds.Right
                + ElementProxy.Margin.Left - ElementProxy.Margin.Right;

            return newBounds;
        }
    }
}
