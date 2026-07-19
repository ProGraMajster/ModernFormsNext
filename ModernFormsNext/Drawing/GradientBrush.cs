using System;
using System.Linq;

namespace ModernFormsNext.Drawing
{
    /// <summary>
    /// Provides observable stops and spread behavior for gradient brushes.
    /// </summary>
    public abstract class GradientBrush : Brush
    {
        private GradientStop[]? orderedStops;
        private GradientSpreadMode spreadMode;

        /// <summary>
        /// Initializes a gradient brush with an observable stop collection and pad spread mode.
        /// </summary>
        protected GradientBrush()
        {
            GradientStops.Changed += HandleGradientStopsChanged;
        }

        /// <summary>
        /// Gets the observable collection of gradient stops.
        /// </summary>
        /// <remarks>
        /// Stops may be stored in any order. Rendering uses a stable sort by offset, preserving
        /// collection order when offsets are equal. Collection and item changes raise
        /// <see cref="Brush.Changed"/>. Mutate the collection on the UI thread while it is in use.
        /// </remarks>
        public GradientStopCollection GradientStops { get; } = new();

        /// <summary>
        /// Gets or sets how colors continue outside the first and last gradient stop.
        /// </summary>
        /// <remarks>
        /// The value maps identically to Skia shader tile behavior on Windows and Android.
        /// Changing it invalidates controls using this brush without affecting layout.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the assigned value is not a defined <see cref="GradientSpreadMode"/>.
        /// </exception>
        public GradientSpreadMode SpreadMode
        {
            get => spreadMode;
            set
            {
                if (!Enum.IsDefined(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The gradient spread mode is not defined.");
                if (spreadMode == value)
                    return;

                spreadMode = value;
                OnChanged(EventArgs.Empty);
            }
        }

        internal GradientStop[] GetOrderedStops()
            => orderedStops ??= GradientStops
                .Select(static (stop, index) => (Stop: stop, Index: index))
                .OrderBy(static item => item.Stop.Offset)
                .ThenBy(static item => item.Index)
                .Select(static item => item.Stop)
                .ToArray();

        private void HandleGradientStopsChanged(object? sender, EventArgs e)
        {
            orderedStops = null;
            OnChanged(EventArgs.Empty);
        }
    }
}
