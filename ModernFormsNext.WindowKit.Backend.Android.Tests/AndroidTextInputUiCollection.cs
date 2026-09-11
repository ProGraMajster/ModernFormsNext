namespace ModernFormsNext.WindowKit.Backend.Android.Tests;

/// <summary>Serializes text input tests that use the framework's process-wide UI services.</summary>
/// <remarks>
/// Real editors initialize shared cursor/theme services. These adapter tests must not initialize
/// them concurrently with other test classes; their input routing and assertions remain unchanged.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AndroidTextInputUiCollection
{
    /// <summary>Identifies the collection for Android adapter tests using real framework editors.</summary>
    public const string Name = "Android text input UI";
}
