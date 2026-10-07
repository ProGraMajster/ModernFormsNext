using System.Collections.ObjectModel;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Notifications;

/// <summary>Platform-neutral defensive bounds, immutable snapshots and advertised-capability degradation.</summary>
/// <remarks>The 256-item/16384-character safety bounds prevent unbounded work; they are not native display limits.
/// Providers separately enforce payload sizes, resource formats and native identifiers.</remarks>
public static class SystemNotificationValidation
{
    /// <summary>Copies all collections and typed extensions before any asynchronous provider work.</summary>
    public static SystemNotification Snapshot(SystemNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification with
        {
            Text = Copy(notification.Text).Select(text => text with { PlatformOptions = Options(text.PlatformOptions) }).ToArray(),
            Images = Copy(notification.Images).Select(Image).ToArray(),
            Actions = Copy(notification.Actions).Select(action => action with
            { InputIds = Copy(action.InputIds), PlatformOptions = Options(action.PlatformOptions) }).ToArray(),
            Inputs = Copy(notification.Inputs).Select(input => input with
            { Choices = Copy(input.Choices), PlatformOptions = Options(input.PlatformOptions) }).ToArray(),
            PlatformOptions = Options(notification.PlatformOptions),
            Sender = notification.Sender is { } sender ? sender with { Image = sender.Image is null ? null : Image(sender.Image) } : null
        };

        static SystemNotificationImage Image(SystemNotificationImage image) => image with { PlatformOptions = Options(image.PlatformOptions) };
        static SystemNotificationOptions Options(SystemNotificationOptions options)
            => (options ?? throw new ArgumentException("Platform options cannot be null.")).Snapshot();
        static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source is null || source.Count > 256) throw new ArgumentException("Notification collection exceeds the common safety bound of 256 items.");
            var copy = source.ToArray();
            if (copy.Any(item => item is null)) throw new ArgumentException("Notification collections cannot contain null items.");
            return copy;
        }
    }

    /// <summary>Returns the logical application key without hashing or native identifier constraints.</summary>
    public static SystemNotificationKey GetKey(SystemNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var key = new SystemNotificationKey(notification.Id, notification.Group);
        ValidateKey(key);
        return key;
    }

    /// <summary>Validates a logical key against common data safety bounds, not OS tag lengths.</summary>
    public static void ValidateKey(SystemNotificationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        CheckString(key.Id, 4096, false);
        CheckString(key.Group, 4096);
    }

    /// <summary>Validates semantic progress without a native sequence or mandatory native status label.</summary>
    public static void ValidateProgress(SystemNotificationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Value is double value && (!double.IsFinite(value) || value < 0 || value > 1))
            throw new ArgumentException("Progress must be finite and between zero and one, or null.");
        CheckString(progress.Status, 16384);
        CheckString(progress.Title, 16384);
        CheckString(progress.ValueText, 16384);
    }

    /// <summary>Validates semantics and applies only advertised backend features/limits. Performs no native or file I/O.</summary>
    public static SystemNotification Normalize(SystemNotification notification, SystemNotificationCapabilities capabilities,
        IList<SystemNotificationWarning> warnings)
    {
        _ = GetKey(notification);
        CheckString(notification.Title, 16384); CheckString(notification.Message, 16384); CheckString(notification.ActivationData, 16384);
        if (!Enum.IsDefined(notification.Urgency)) throw new ArgumentException("Unknown urgency.");
        if (string.IsNullOrWhiteSpace(notification.Title) && string.IsNullOrWhiteSpace(notification.Message) && notification.Text.Count == 0)
            throw new ArgumentException("A notification requires text.");
        foreach (var block in notification.Text)
        {
            CheckString(block.Content, 16384);
            if (block.Language is not null) _ = System.Globalization.CultureInfo.GetCultureInfo(block.Language);
        }
        if (notification.Progress is not null) ValidateProgress(notification.Progress);
        foreach (var image in notification.Images) ValidateImage(image);
        if (notification.Sender is { } person)
        {
            CheckString(person.Id, 4096, false); CheckString(person.DisplayName, 16384, false);
            if (person.Image is not null) ValidateImage(person.Image);
        }
        var inputIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in notification.Inputs)
        {
            CheckString(input.Id, 4096, false);
            if (!inputIds.Add(input.Id)) throw new ArgumentException("Input identifiers must be unique.");
            if (input.Title is not null) CheckString(input.Title, 16384);
            if (input.Placeholder is not null) CheckString(input.Placeholder, 16384);
            if (input.DefaultValue is not null) CheckString(input.DefaultValue, 16384);
            var choices = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choice in input.Choices)
            {
                CheckString(choice.Id, 4096, false); CheckString(choice.Title, 16384, false);
                if (!choices.Add(choice.Id)) throw new ArgumentException("Choice identifiers must be unique.");
            }
            if (choices.Count > 0 && input.DefaultValue is not null && !choices.Contains(input.DefaultValue))
                throw new ArgumentException("The default selection must identify an existing choice.");
        }
        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in notification.Actions)
        {
            CheckString(action.Title, 16384); CheckString(action.ActivationData, 16384); CheckString(action.Id, 4096);
            if (action.Id.Length > 0 && !actionIds.Add(action.Id)) throw new ArgumentException("Explicit action identifiers must be unique.");
            if (!Enum.IsDefined(action.ActivationType)) throw new ArgumentException("Unknown action intent.");
            if (action.ActivationType == SystemNotificationActivationType.OpenUri && action.TargetUri?.IsAbsoluteUri != true)
                throw new ArgumentException("OpenUri actions require an absolute TargetUri.");
            if (action.InputIds.Distinct(StringComparer.Ordinal).Count() != action.InputIds.Count || action.InputIds.Any(id => !inputIds.Contains(id)))
                throw new ArgumentException("Action inputs must reference distinct declared inputs.");
        }

        var limits = capabilities.Limits;
        var inputs = Limit(notification.Inputs.Where(input =>
        {
            if (input.Choices.Count > 0 && limits.Choices == 0) { warnings.Add(new("ChoiceCount", "Selection inputs cannot be represented with zero available choices.")); return false; }
            return Keep(input.Choices.Count == 0
            ? SystemNotificationFeatures.TextInput : SystemNotificationFeatures.SelectionInput, "Inputs");
        }).ToArray(), limits.Inputs, "InputCount")
            .Select(input =>
            {
                var choices = Limit(input.Choices, limits.Choices, "ChoiceCount");
                return input with { Choices = choices, DefaultValue = input.Choices.Count > 0 && input.DefaultValue is not null &&
                    !choices.Any(choice => choice.Id == input.DefaultValue) ? null : input.DefaultValue };
            }).ToArray();
        var retainedInputs = new HashSet<string>(inputs.Select(input => input.Id), StringComparer.Ordinal);
        var actions = Limit(notification.Actions.Where(action =>
        {
            if (!Keep(SystemNotificationFeatures.Actions, "Actions") || action.ActivationType == SystemNotificationActivationType.OpenUri &&
                !Keep(SystemNotificationFeatures.UriActions, "UriActions")) return false;
            if (action.InputIds.Any(id => !retainedInputs.Contains(id)) || limits.InputsPerAction is int count && action.InputIds.Count > count)
            { warnings.Add(new("ActionInputs", "An action requiring unavailable response inputs was removed.")); return false; }
            return true;
        }).ToArray(), limits.Actions, "ActionCount");
        var title = notification.Title; var message = notification.Message; var text = notification.Text;
        if (limits.TextBlocks is int blocks)
        {
            if (blocks < 1) throw new ArgumentException("A text-capable backend must allow at least one block.");
            if (title.Length > 0) blocks--;
            if (message.Length > 0)
            {
                if (blocks > 0) blocks--;
                else { message = ""; warnings.Add(new("TextCount", "The body exceeded the backend's text block limit.")); }
            }
            text = Limit(text, blocks, "TextCount");
        }
        return notification with
        {
            Title = title, Message = message, Text = text,
            Images = notification.Images.Count == 0 || Keep(SystemNotificationFeatures.Images, "Images")
                ? Limit(notification.Images, limits.Images, "ImageCount") : [],
            Actions = actions, Inputs = inputs,
            Progress = notification.Progress is null || Keep(SystemNotificationFeatures.Progress, "Progress") &&
                (notification.Progress.Value is not null || Keep(SystemNotificationFeatures.IndeterminateProgress, "IndeterminateProgress")) ? notification.Progress : null,
            Timestamp = notification.Timestamp is null || Keep(SystemNotificationFeatures.Timestamp, "Timestamp") ? notification.Timestamp : null,
            ExpiresAt = notification.ExpiresAt is null || Keep(SystemNotificationFeatures.Expiration, "Expiration") ? notification.ExpiresAt : null,
            Urgency = notification.Urgency == SystemNotificationUrgency.Default || Keep(SystemNotificationFeatures.Urgency, "Urgency") ? notification.Urgency : SystemNotificationUrgency.Default,
            Sender = notification.Sender is null || Keep(SystemNotificationFeatures.SenderMetadata, "SenderMetadata") ? notification.Sender : null
        };

        bool Keep(SystemNotificationFeatures feature, string code)
        {
            if (capabilities.Supports(feature)) return true;
            warnings.Add(new(code, $"The selected backend does not support {feature}; optional content was removed."));
            return false;
        }
        IReadOnlyList<T> Limit<T>(IReadOnlyList<T> values, int? maximum, string code)
        {
            if (maximum is null || values.Count <= maximum) return values;
            if (maximum < 0) throw new ArgumentException("Backend limits cannot be negative.");
            warnings.Add(new(code, $"Content was limited to the backend's advertised maximum of {maximum} items."));
            return values.Take(maximum.Value).ToArray();
        }
    }

    private static void ValidateImage(SystemNotificationImage image)
    {
        CheckString(image.Source, 16384, false);
        if (!Enum.IsDefined(image.Role)) throw new ArgumentException("Unknown image role.");
        if (image.AlternateText is not null) CheckString(image.AlternateText, 16384);
    }

    /// <summary>Copies and defensively bounds responses before native callbacks enter the dispatcher/lifecycle.</summary>
    public static SystemNotificationActivation CopyActivation(SystemNotificationActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        CheckString(activation.NotificationId, 4096); CheckString(activation.Group, 4096); CheckString(activation.ActivationData, 16384);
        if (activation.ActionId is not null) CheckString(activation.ActionId, 4096);
        if (activation.UserInput is null || activation.UserInput.Count > 256) throw new ArgumentException("Too many activation input values.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in activation.UserInput)
        { CheckString(pair.Key, 4096, false); CheckString(pair.Value, 16384); values.Add(pair.Key, pair.Value); }
        return activation with { UserInput = new ReadOnlyDictionary<string, string>(values), PlatformData = activation.PlatformData.Snapshot() };
    }

    /// <summary>Validates bounded Unicode without imposing an XML schema or including content in exceptions.</summary>
    public static void CheckString(string value, int maximumLength, bool allowEmpty = true)
    {
        if (value is null || value.Length > maximumLength || !allowEmpty && string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A notification string is missing or exceeds its documented safety bound.");
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\0' || char.IsLowSurrogate(value[i])) throw new ArgumentException("Notification text contains invalid Unicode or NUL.");
            if (char.IsHighSurrogate(value[i]) && (++i == value.Length || !char.IsLowSurrogate(value[i])))
                throw new ArgumentException("Notification text contains invalid Unicode.");
        }
    }
}
