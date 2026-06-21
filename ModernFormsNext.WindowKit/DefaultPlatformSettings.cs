using System;
using ModernFormsNext.WindowKit.Input;
using ModernFormsNext.WindowKit.Input.Platform;
//using ModernFormsNext.WindowKit.Media;
using ModernFormsNext.WindowKit.Metadata;
//using ModernFormsNext.WindowKit.VisualTree;

namespace ModernFormsNext.WindowKit.Platform
{
    /// <summary>
    /// A default implementation of <see cref="IPlatformSettings"/> for platforms.
    /// </summary>
    [PrivateApi]
    public class DefaultPlatformSettings : IPlatformSettings
    {
        /// <inheritdoc />
        public virtual Size GetTapSize(PointerType type)
        {
            return type switch
            {
                PointerType.Touch => new(10, 10),
                _ => new(4, 4),
            };
        }

        /// <inheritdoc />
        public virtual Size GetDoubleTapSize(PointerType type)
        {
            return type switch
            {
                PointerType.Touch => new(16, 16),
                _ => new(4, 4),
            };
        }

        /// <inheritdoc />
        public virtual TimeSpan GetDoubleTapTime(PointerType type) => TimeSpan.FromMilliseconds(500);

        /// <inheritdoc />
        public virtual TimeSpan HoldWaitDuration => TimeSpan.FromMilliseconds(300);

        //public PlatformHotkeyConfiguration HotkeyConfiguration =>
        //    AvaloniaLocator.Current.GetRequiredService<PlatformHotkeyConfiguration>();

        /// <inheritdoc />
        public virtual PlatformColorValues GetColorValues()
        {
            return new PlatformColorValues
            {
                ThemeVariant = PlatformThemeVariant.Light
            };
        }

        /// <inheritdoc />
        public virtual event EventHandler<PlatformColorValues>? ColorValuesChanged;

        /// <summary>
        /// Raises <see cref="ColorValuesChanged"/> with the latest platform color values.
        /// </summary>
        /// <param name="colorValues">The color values reported by the platform.</param>
        protected void OnColorValuesChanged(PlatformColorValues colorValues)
        {
            ColorValuesChanged?.Invoke(this, colorValues);
        }
    }
}
