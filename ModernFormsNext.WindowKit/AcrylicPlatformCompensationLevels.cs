namespace ModernFormsNext.WindowKit.Controls
{
    /// <summary>
    /// Defines compensation levels for the platform depending on the transparency level.
    /// It controls the base opacity level of the 'tracing paper' layer that compensates
    /// for low blur radius.
    /// </summary>
    public record struct AcrylicPlatformCompensationLevels
    {
        /// <summary>
        /// Initializes a new set of platform compensation levels.
        /// </summary>
        /// <param name="transparent">The compensation opacity used for transparent windows.</param>
        /// <param name="blurred">The compensation opacity used for blur-behind windows.</param>
        /// <param name="acrylic">The compensation opacity used for acrylic blur windows.</param>
        public AcrylicPlatformCompensationLevels(double transparent, double blurred, double acrylic)
        {
            TransparentLevel = transparent;
            BlurLevel = blurred;
            AcrylicBlurLevel = acrylic;
        }

        /// <summary>
        /// Gets the compensation opacity used when the window background is transparent.
        /// </summary>
        public double TransparentLevel { get; }

        /// <summary>
        /// Gets the compensation opacity used when the window background uses blur-behind.
        /// </summary>
        public double BlurLevel { get; }

        /// <summary>
        /// Gets the compensation opacity used when the window background uses acrylic blur.
        /// </summary>
        public double AcrylicBlurLevel { get; }
    }
}
