namespace ModernFormsNext.Testing;

/// <summary>Describes a deterministic logical viewport and render scale for a headless window.</summary>
public readonly record struct TestViewport
{
    /// <summary>Initializes a deterministic viewport.</summary>
    /// <param name="width">The logical width in pixels.</param>
    /// <param name="height">The logical height in pixels.</param>
    /// <param name="renderScale">The logical-to-device scale, where 1 is 100 percent.</param>
    public TestViewport(int width, int height, double renderScale = 1d)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (!double.IsFinite(renderScale) || renderScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(renderScale), renderScale, "Render scale must be finite and greater than zero.");

        Width = width;
        Height = height;
        RenderScale = renderScale;
    }

    /// <summary>Gets the logical viewport width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the logical viewport height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the controlled logical-to-device render scale.</summary>
    public double RenderScale { get; }

    /// <summary>Returns a viewport with a different logical size.</summary>
    /// <param name="width">The new logical width.</param>
    /// <param name="height">The new logical height.</param>
    /// <returns>The updated immutable viewport.</returns>
    public TestViewport Resize(int width, int height) => new(width, height, RenderScale);

    /// <summary>Returns a viewport with a different controlled render scale.</summary>
    /// <param name="renderScale">The new logical-to-device scale.</param>
    /// <returns>The updated immutable viewport.</returns>
    public TestViewport WithRenderScale(double renderScale) => new(Width, Height, renderScale);
}
