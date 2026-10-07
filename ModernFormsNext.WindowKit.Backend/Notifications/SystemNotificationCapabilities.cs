namespace ModernFormsNext.Notifications;

/// <summary>Semantic backend abilities. These flags never encode a particular OS SDK or current permission.</summary>
[Flags]
public enum SystemNotificationFeatures : ulong
{
    /// <summary>No native notification functionality.</summary>
    None = 0,
    /// <summary>Basic title and message.</summary>
    Basic = 1UL << 0,
    /// <summary>Image content; exact roles/presentation depend on the backend.</summary>
    Images = 1UL << 1,
    /// <summary>HTTP(S) images fetched by the native platform.</summary>
    RemoteImages = 1UL << 2,
    /// <summary>Platform-resolved packaged or application-resource images.</summary>
    ResourceImages = 1UL << 3,
    /// <summary>Application action responses.</summary>
    Actions = 1UL << 4,
    /// <summary>Text response input, including direct reply.</summary>
    TextInput = 1UL << 5,
    /// <summary>Selection response input.</summary>
    SelectionInput = 1UL << 6,
    /// <summary>Both text and selection inputs. Query TextInput alone for a plain reply action.</summary>
    Inputs = TextInput | SelectionInput,
    /// <summary>Determinate progress presentation.</summary>
    Progress = 1UL << 7,
    /// <summary>Updates existing notification progress under the same identity. Native protocols may differ.</summary>
    LiveUpdates = 1UL << 8,
    /// <summary>Replacing submitted content while retaining its logical identity.</summary>
    Replacement = 1UL << 9,
    /// <summary>Native grouping or identity partitioning; group removal is advertised separately.</summary>
    Grouping = 1UL << 10,
    /// <summary>Enumeration of this application's actually delivered native notifications.</summary>
    History = 1UL << 11,
    /// <summary>Custom sound assets through the platform's supported source mechanism.</summary>
    CustomAudio = 1UL << 12,
    /// <summary>Custom displayed event time.</summary>
    Timestamp = 1UL << 13,
    /// <summary>Activation can launch an application process that is not running.</summary>
    ColdActivation = 1UL << 14,
    /// <summary>Native dismissal or failure callbacks while the service is active.</summary>
    DismissalEvents = 1UL << 15,
    /// <summary>Native expiration policy without an emulated application timer.</summary>
    Expiration = 1UL << 16,
    /// <summary>Indeterminate progress presentation.</summary>
    IndeterminateProgress = 1UL << 17,
    /// <summary>Actions asking the OS to open a URI.</summary>
    UriActions = 1UL << 18,
    /// <summary>Higher attention preference, subject to system/user policy.</summary>
    Urgency = 1UL << 19,
    /// <summary>Semantic person/sender metadata, beyond rendering a portrait as an ordinary image.</summary>
    SenderMetadata = 1UL << 20,
    /// <summary>Removal by logical key or Id, without requiring native history enumeration.</summary>
    Removal = 1UL << 21,
    /// <summary>Removal of all entries in a logical group.</summary>
    GroupRemoval = 1UL << 22,
    /// <summary>Clearing the application notification store.</summary>
    Clear = 1UL << 23
}

/// <summary>Base for immutable platform-specific capability snapshots. No platform dependency enters common code.</summary>
public abstract record SystemNotificationPlatformCapabilities;

/// <summary>Backend content limits, separate from the common contract's defensive allocation bounds.</summary>
/// <remarks>Null means that no fixed count is advertised, not that OS payload size is unlimited.
/// Limits may depend on registration or the chosen native presentation path.</remarks>
public sealed record SystemNotificationLimits
{
    /// <summary>Gets the maximum total of nonempty Title, Message and additional Text blocks.</summary>
    public int? TextBlocks { get; init; }
    /// <summary>Gets the maximum image count.</summary>
    public int? Images { get; init; }
    /// <summary>Gets the maximum action count.</summary>
    public int? Actions { get; init; }
    /// <summary>Gets the maximum input count.</summary>
    public int? Inputs { get; init; }
    /// <summary>Gets the maximum selection choices per input.</summary>
    public int? Choices { get; init; }
    /// <summary>Gets the maximum inputs associated with an individual action.</summary>
    public int? InputsPerAction { get; init; }
}

/// <summary>Immutable backend abilities, independent of authorization and current availability.</summary>
/// <param name="Backend">Selected transport or diagnostic provider name.</param>
/// <param name="Features">Supported semantic features.</param>
/// <param name="Reason">Optional capability/fallback explanation, not a permission result.</param>
public sealed record SystemNotificationCapabilities(string Backend, SystemNotificationFeatures Features, string? Reason = null)
{
    /// <summary>Gets the unavailable-service capability snapshot.</summary>
    public static SystemNotificationCapabilities Unavailable { get; } = new("Unavailable", SystemNotificationFeatures.None, "No system notification service is registered.");
    /// <summary>Gets limits for this selected backend.</summary>
    public SystemNotificationLimits Limits { get; init; } = new();
    /// <summary>Gets the selected platform's additional capabilities, or null for a common-only provider.</summary>
    public SystemNotificationPlatformCapabilities? Platform { get; init; }
    /// <summary>Gets a typed platform capability snapshot, or null when the current provider is not that platform.</summary>
    public T? GetPlatform<T>() where T : SystemNotificationPlatformCapabilities => Platform as T;
    /// <summary>Gets whether basic notifications are implemented, even if the user currently denies permission.</summary>
    public bool IsSupported => Supports(SystemNotificationFeatures.Basic);
    /// <summary>Tests all requested semantic feature bits.</summary>
    public bool Supports(SystemNotificationFeatures features) => (Features & features) == features;
}
