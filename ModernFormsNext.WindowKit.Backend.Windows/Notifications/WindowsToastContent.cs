using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;

namespace ModernFormsNext.WindowKit.Backend.Windows.Notifications;

/// <summary>Validated native XML and delivery options, shared by the two toast transports.</summary>
/// <param name="Xml">Bounded, escaped native toast XML.</param>
/// <param name="Key">Application replacement identity; providers map its group with <see cref="WindowsToastContent.ToNativeGroup"/>.</param>
/// <param name="Notification">Normalized common content.</param>
/// <param name="Options">Normalized Windows delivery options.</param>
/// <param name="Warnings">Degradation diagnostics.</param>
public sealed record WindowsToastContent(string Xml, SystemNotificationKey Key, SystemNotification Notification,
    WindowsSystemNotificationOptions Options, IReadOnlyList<SystemNotificationWarning> Warnings)
{
    private const string DefaultNativeGroup = "mfn.default";

    /// <summary>Maps a logical ID to a stable 96-bit SHA-256 prefix encoded in 16 native characters.</summary>
    /// <remarks>The mapping is deterministic across processes. As with any truncated hash, collisions are theoretically possible.</remarks>
    public static string ToNativeTag(string id)
    {
        SystemNotificationValidation.CheckString(id, 4096, false);
        return HashNativeIdentity(id);
    }

    /// <summary>Maps a logical group to the reserved empty-group token or a stable native hash.</summary>
    /// <remarks>Even the logical string mfn.default is hashed, so it cannot alias the empty group.</remarks>
    public static string ToNativeGroup(string group)
    {
        SystemNotificationValidation.CheckString(group, 4096);
        return group.Length == 0 ? DefaultNativeGroup : HashNativeIdentity(group);
    }

    // Group allows whitespace-only values; do not apply the stricter nonblank Id validation here.
    private static string HashNativeIdentity(string value)
        => Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)), 0, 12)
            .Replace('+', '-').Replace('/', '_');

    /// <summary>Recovers logical identity from managed payloads and retains an opaque removal reference for all native entries.</summary>
    public static SystemNotificationHistoryEntry ReadHistory(string xml, string tag, string group, Guid owner)
    {
        SystemNotificationKey? key = null;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16384 });
            var launch = (string?)XDocument.Load(reader).Root?.Attribute("launch");
            if (launch?.StartsWith("mfn=1&", StringComparison.Ordinal) == true)
            {
                var activation = DecodeActivation(launch, new Dictionary<string, string>());
                if (activation.NotificationId.Length > 0 && ToNativeTag(activation.NotificationId) == tag && ToNativeGroup(activation.Group) == group)
                    key = new(activation.NotificationId, activation.Group);
            }
        }
        catch (Exception e) when (e is ArgumentException or XmlException) { /* Foreign payloads retain their opaque native identity. */ }
        return new(key, new WindowsSystemNotificationReference(owner, tag, group));
    }

    /// <summary>Builds native XML. Local image reads may block; call from a worker thread.</summary>
    /// <remarks>Remote images are left to the OS; this method never performs HTTP requests.</remarks>
    public static WindowsToastContent Create(SystemNotification notification, SystemNotificationCapabilities capabilities, bool legacyTemplate = false)
    {
        var warnings = new List<SystemNotificationWarning>();
        var n = SystemNotificationValidation.Normalize(notification, capabilities, warnings);
        // These are Windows adaptive schema/payload bounds, deliberately absent from common validation.
        SystemNotificationValidation.CheckString(n.Title, 2048); SystemNotificationValidation.CheckString(n.Message, 2048);
        SystemNotificationValidation.CheckString(n.ActivationData, 2048);
        foreach (var block in n.Text) SystemNotificationValidation.CheckString(block.Content, 2048);
        if (n.Progress is not null) ValidateProgress(n.Progress);
        foreach (var input in n.Inputs)
        {
            SystemNotificationValidation.CheckString(input.Id, 64, false);
            if (input.Title is not null) SystemNotificationValidation.CheckString(input.Title, 256);
            if (input.Placeholder is not null) SystemNotificationValidation.CheckString(input.Placeholder, 256);
            if (input.DefaultValue is not null) SystemNotificationValidation.CheckString(input.DefaultValue, 1024);
            foreach (var choice in input.Choices)
            { SystemNotificationValidation.CheckString(choice.Id, 64, false); SystemNotificationValidation.CheckString(choice.Title, 256, false); }
        }
        foreach (var action in n.Actions)
        {
            SystemNotificationValidation.CheckString(action.Title, 256);
            SystemNotificationValidation.CheckString(action.Id, 256);
            SystemNotificationValidation.CheckString(action.ActivationData, 2048);
            if (action.Id.Length == 0 && action.ActivationData.Length > 256) throw new ArgumentException("Long action payloads require an explicit action ID.");
        }
        var windows = capabilities.GetPlatform<WindowsSystemNotificationCapabilities>() ?? new(WindowsSystemNotificationFeatures.None);
        var options = n.PlatformOptions.Get<WindowsSystemNotificationOptions>() ?? new();
        if (n.Urgency == SystemNotificationUrgency.High) options = options with { HighPriority = true };
        if (n.Urgency == SystemNotificationUrgency.Low) warnings.Add(new("Urgency", "Windows toast priority does not represent Low urgency; default priority is used."));
        if (!Enum.IsDefined(options.Scenario) || !Enum.IsDefined(options.Duration)) throw new ArgumentException("Unknown scenario or duration.");
        if (options.Language is not null) _ = CultureInfo.GetCultureInfo(options.Language);
        if (options.RemoteId is not null) SystemNotificationValidation.CheckString(options.RemoteId, 64, false);
        if (options.Silent && (options.Audio is not null || options.LoopAudio)) throw new ArgumentException("Silent audio cannot specify a source or loop.");
        if (options.LoopAudio && options.Duration != WindowsNotificationDuration.Long) throw new ArgumentException("Looping audio requires Long duration.");
        if (options.Scenario is WindowsNotificationScenario.Alarm or WindowsNotificationScenario.Reminder && n.Actions.Count == 0)
            throw new ArgumentException("Alarm/reminder notifications require an action.");
        options = options with
        {
            Attribution = options.Attribution is null || KeepWindows(WindowsSystemNotificationFeatures.Attribution) ? options.Attribution : null,
            Header = options.Header is null || KeepWindows(WindowsSystemNotificationFeatures.Headers) ? options.Header : null,
            Scenario = options.Scenario == WindowsNotificationScenario.Default || KeepWindows(options.Scenario == WindowsNotificationScenario.Urgent
                ? WindowsSystemNotificationFeatures.UrgentScenario : WindowsSystemNotificationFeatures.Scenarios) ? options.Scenario : WindowsNotificationScenario.Default,
            SuppressPopup = options.SuppressPopup && KeepWindows(WindowsSystemNotificationFeatures.SuppressPopup),
            HighPriority = options.HighPriority && KeepWindows(WindowsSystemNotificationFeatures.Priority),
            ExpiresOnReboot = options.ExpiresOnReboot is null || KeepWindows(WindowsSystemNotificationFeatures.RebootExpiration) ? options.ExpiresOnReboot : null,
            AllowMirroring = options.AllowMirroring is null || KeepWindows(WindowsSystemNotificationFeatures.Mirroring) ? options.AllowMirroring : null,
            RemoteId = options.RemoteId is null || KeepWindows(WindowsSystemNotificationFeatures.Mirroring) ? options.RemoteId : null,
            LoopAudio = options.LoopAudio && KeepWindows(WindowsSystemNotificationFeatures.LoopingAudio)
        };
        if (options.Balloon is not null) warnings.Add(new("BalloonOptions", "Shell balloon options do not apply to toast notifications."));
        if (options.Audio is string audio)
        {
            if (!Uri.TryCreate(audio, UriKind.Absolute, out var audioUri)) throw new ArgumentException("Audio must be an absolute supported URI.");
            if (audioUri.Scheme == "ms-winsoundevent")
            {
                var sound = audio["ms-winsoundevent:".Length..];
                if (!ValidSystemSound(sound)) throw new ArgumentException("Unknown Windows sound event.");
                if (options.LoopAudio && !sound.StartsWith("Notification.Looping.", StringComparison.Ordinal))
                    throw new ArgumentException("Looping audio requires a looping system sound.");
            }
            else if (audioUri.Scheme is "ms-appx" or "ms-resource")
            {
                if (!Keep(SystemNotificationFeatures.CustomAudio)) options = options with { Audio = null, LoopAudio = false };
                else if (audioUri.Scheme == "ms-appx" && !new[] { ".aac", ".flac", ".m4a", ".mp3", ".wav", ".wma" }.Contains(Path.GetExtension(audioUri.AbsolutePath), StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Unsupported custom audio format.");
            }
            else throw new ArgumentException("Custom audio requires ms-appx or ms-resource; remote, app-data and local file audio are unsupported.");
        }
        if (options.RawXml is string raw)
        {
            if (legacyTemplate || !windows.Supports(WindowsSystemNotificationFeatures.RawXml | options.RequiredFeatures) || !capabilities.Supports(options.RequiredCommonFeatures))
                throw new NotSupportedException("Raw XML requires explicitly available capabilities and an adaptive toast backend.");
            var parsed = ParseRaw(raw);
            RejectDegradation();
            return new(parsed, SystemNotificationValidation.GetKey(n), n, options, warnings.AsReadOnly());
        }
        var toast = new XElement("toast", new XAttribute("launch", EncodeActivation(n.Id, n.ActivationData, null, n.Group)));
        if (options.Duration != WindowsNotificationDuration.Default) toast.SetAttributeValue("duration", options.Duration.ToString().ToLowerInvariant());
        if (n.Timestamp is { } timestamp) toast.SetAttributeValue("displayTimestamp", timestamp.ToString("O", CultureInfo.InvariantCulture));
        if (options.Scenario != WindowsNotificationScenario.Default) toast.SetAttributeValue("scenario", options.Scenario == WindowsNotificationScenario.IncomingCall ? "incomingCall" : options.Scenario.ToString().ToLowerInvariant());
        var visual = new XElement("visual");
        if (options.Language is not null) visual.SetAttributeValue("lang", options.Language);
        var binding = new XElement("binding", new XAttribute("template", "ToastGeneric"));
        var text = new List<SystemNotificationText>();
        if (n.Title.Length > 0) text.Add(new(n.Title));
        if (n.Message.Length > 0) text.Add(new(n.Message));
        text.AddRange(n.Text);
        for (int i = 0; i < text.Count; i++)
        {
            var block = text[i];
            var maxLines = block.PlatformOptions.Get<WindowsSystemNotificationTextOptions>()?.MaxLines;
            if (maxLines is < 1 or > 4) throw new ArgumentException("Windows text supports one through four lines.");
            if (i == 0 && maxLines > 2) throw new ArgumentException("The title supports at most two lines.");
            var element = new XElement("text", block.Content);
            if (legacyTemplate) element.SetAttributeValue("id", i + 1);
            else if (maxLines is int lines) element.SetAttributeValue("hint-maxLines", lines);
            if (block.Language is not null) element.SetAttributeValue("lang", block.Language);
            binding.Add(element);
        }
        if (options.Attribution is not null) binding.Add(new XElement("text", new XAttribute("placement", "attribution"), options.Attribution));
        int imageCount = 0;
        var placements = new HashSet<WindowsNotificationImagePlacement>();
        foreach (var img in n.Images)
        {
            var imageOptions = img.PlatformOptions.Get<WindowsSystemNotificationImageOptions>() ?? new();
            if (!Enum.IsDefined(imageOptions.Placement)) throw new ArgumentException("Unknown Windows image placement.");
            var requested = imageOptions.Placement != WindowsNotificationImagePlacement.Default ? imageOptions.Placement : img.Role switch
            {
                SystemNotificationImageRole.Thumbnail => WindowsNotificationImagePlacement.Hero,
                SystemNotificationImageRole.Identity => WindowsNotificationImagePlacement.AppLogo,
                SystemNotificationImageRole.Portrait => WindowsNotificationImagePlacement.Avatar,
                _ => WindowsNotificationImagePlacement.Inline
            };
            var placement = requested == WindowsNotificationImagePlacement.Avatar ? WindowsNotificationImagePlacement.AppLogo : requested;
            var feature = placement switch { WindowsNotificationImagePlacement.Hero => WindowsSystemNotificationFeatures.HeroImages,
                WindowsNotificationImagePlacement.AppLogo => WindowsSystemNotificationFeatures.AppLogo, _ => WindowsSystemNotificationFeatures.InlineImages };
            if (!legacyTemplate && !KeepWindows(feature)) placement = WindowsNotificationImagePlacement.Inline;
            if (!placements.Add(placement)) { warnings.Add(new("ImagePlacement", "Windows allows one image per placement; an additional image was removed.")); continue; }
            var source = ImageSource(img.Source);
            if (source is null) continue;
            if (legacyTemplate && imageCount > 0) { warnings.Add(new("LegacyImage", "Legacy templates allow one image.")); continue; }
            var element = new XElement("image", new XAttribute("src", source));
            if (legacyTemplate) element.SetAttributeValue("id", 1);
            else if (placement != WindowsNotificationImagePlacement.Inline) element.SetAttributeValue("placement", placement == WindowsNotificationImagePlacement.Hero ? "hero" : "appLogoOverride");
            if (!legacyTemplate && requested == WindowsNotificationImagePlacement.Avatar) element.SetAttributeValue("hint-crop", "circle");
            if (img.AlternateText is not null) element.SetAttributeValue("alt", img.AlternateText);
            if (imageOptions.AddImageQuery) element.SetAttributeValue("addImageQuery", "true");
            binding.Add(element);
            imageCount++;
        }
        if (legacyTemplate) binding.SetAttributeValue("template", (imageCount > 0 ? "ToastImageAndText" : "ToastText") + (text.Count == 1 ? "01" : text.Count == 2 ? "02" : "04"));
        if (n.Progress is not null)
            binding.Add(new XElement("progress", new XAttribute("title", "{progressTitle}"), new XAttribute("value", "{progressValue}"),
                new XAttribute("status", "{progressStatus}"), new XAttribute("valueStringOverride", "{progressValueString}")));
        visual.Add(binding);
        toast.Add(visual);
        if (options.Header is { } header)
        {
            SystemNotificationValidation.CheckString(header.Id, 256, false);
            SystemNotificationValidation.CheckString(header.Title, 1024, false);
            SystemNotificationValidation.CheckString(header.Arguments, 2048);
            toast.Add(new XElement("header", new XAttribute("id", header.Id), new XAttribute("title", header.Title),
                new XAttribute("arguments", EncodeActivation(n.Id, header.Arguments, "header", n.Group)), new XAttribute("activationType", "foreground")));
        }
        if (n.Actions.Count > 0 || n.Inputs.Count > 0)
        {
            var actions = new XElement("actions");
            foreach (var input in n.Inputs)
            {
                var element = new XElement("input", new XAttribute("id", input.Id), new XAttribute("type", input.Choices.Count > 0 ? "selection" : "text"));
                if (input.Title is not null) element.SetAttributeValue("title", input.Title);
                if (input.Placeholder is not null && input.Choices.Count == 0) element.SetAttributeValue("placeHolderContent", input.Placeholder);
                if (input.DefaultValue is not null) element.SetAttributeValue("defaultInput", input.DefaultValue);
                foreach (var choice in input.Choices) element.Add(new XElement("selection", new XAttribute("id", choice.Id), new XAttribute("content", choice.Title)));
                actions.Add(element);
            }
            foreach (var action in n.Actions)
            {
                var a = action.PlatformOptions.Get<WindowsNotificationActionOptions>() ?? new();
                if (!Enum.IsDefined(a.Style) || !Enum.IsDefined(a.ActivationType)) throw new ArgumentException("Unknown Windows action option.");
                if (a.InputId is not null && !n.Inputs.Any(i => i.Id == a.InputId)) throw new ArgumentException("Action references a missing input.");
                var nativeActivation = a.ActivationType == WindowsNotificationActivationType.Default
                    ? action.ActivationType == SystemNotificationActivationType.OpenUri ? WindowsNotificationActivationType.Protocol : WindowsNotificationActivationType.Foreground
                    : a.ActivationType;
                if (nativeActivation == WindowsNotificationActivationType.Background)
                { warnings.Add(new("BackgroundActivation", "Desktop COM providers do not implement UWP background tasks; the action was removed.")); continue; }
                if (nativeActivation == WindowsNotificationActivationType.Snooze && options.Scenario is not (WindowsNotificationScenario.Alarm or WindowsNotificationScenario.Reminder))
                    throw new ArgumentException("Snooze requires Alarm or Reminder.");
                if (nativeActivation == WindowsNotificationActivationType.Protocol && action.TargetUri?.IsAbsoluteUri != true)
                    throw new ArgumentException("Protocol activation requires an absolute TargetUri.");
                var activationType = nativeActivation switch
                {
                    WindowsNotificationActivationType.Protocol => "protocol",
                    WindowsNotificationActivationType.Dismiss or WindowsNotificationActivationType.Snooze => "system",
                    _ => "foreground"
                };
                var args = nativeActivation switch
                {
                    WindowsNotificationActivationType.Protocol => action.TargetUri!.AbsoluteUri,
                    WindowsNotificationActivationType.Dismiss => "dismiss",
                    WindowsNotificationActivationType.Snooze => "snooze",
                    _ => EncodeActivation(n.Id, action.ActivationData, action.Id.Length > 0 ? action.Id : action.ActivationData, n.Group)
                };
                var element = new XElement("action", new XAttribute("content", action.Title), new XAttribute("arguments", args), new XAttribute("activationType", activationType));
                if (a.ContextMenu && KeepWindows(WindowsSystemNotificationFeatures.ContextMenuActions)) element.SetAttributeValue("placement", "contextMenu");
                if (a.Style != WindowsNotificationButtonStyle.Default && KeepWindows(WindowsSystemNotificationFeatures.ButtonStyles))
                {
                    toast.SetAttributeValue("useButtonStyle", "true");
                    element.SetAttributeValue("hint-buttonStyle", a.Style.ToString());
                }
                if (a.ToolTip is not null && KeepWindows(WindowsSystemNotificationFeatures.ButtonTooltips)) element.SetAttributeValue("hint-toolTip", a.ToolTip);
                if (a.Icon is not null && ImageSource(a.Icon) is string icon) element.SetAttributeValue("imageUri", icon);
                if (a.InputId is not null) element.SetAttributeValue("hint-inputId", a.InputId);
                // pendingUpdate is a background-activation feature. Desktop foreground COM callbacks
                // do not implement the UWP background task contract, so never emit a misleading hint.
                if (a.PendingUpdate) warnings.Add(new("PendingUpdate", "Desktop activation does not support UWP background pending-update behavior."));
                if (a.TargetApplicationPfn is not null && nativeActivation == WindowsNotificationActivationType.Protocol)
                    element.SetAttributeValue("activationOptions-protocolActivationTargetApplicationPfn", a.TargetApplicationPfn);
                if (action.Title.Length == 0 && (element.Attribute("imageUri") is null || element.Attribute("hint-toolTip") is null))
                    throw new ArgumentException("Icon-only actions require a supported icon and tooltip.");
                actions.Add(element);
            }
            var visible = actions.Elements("action").Where(a => a.Attribute("placement") is null).ToArray();
            if (visible.Any(a => a.Attribute("imageUri") is not null) && visible.Any(a => a.Attribute("imageUri") is null))
            {
                foreach (var action in visible) action.Attribute("imageUri")?.Remove();
                warnings.Add(new("ButtonIcons", "All visible buttons must provide usable icons; icons were removed."));
                if (visible.Any(a => (string?)a.Attribute("content") == "")) throw new ArgumentException("Icon-only button lost its required icon.");
            }
            toast.Add(actions);
        }
        if (options.Silent || options.Audio is not null || options.LoopAudio)
        {
            var audioElement = new XElement("audio");
            if (options.Silent) audioElement.SetAttributeValue("silent", "true");
            if (options.Audio is not null) audioElement.SetAttributeValue("src", options.Audio);
            if (options.LoopAudio) audioElement.SetAttributeValue("loop", "true");
            toast.Add(audioElement);
        }
        var xml = toast.ToString(SaveOptions.DisableFormatting);
        if (Encoding.UTF8.GetByteCount(xml) > 5000) throw new ArgumentException("Toast payload exceeds 5000 UTF-8 bytes.");
        RejectDegradation();
        return new(xml, SystemNotificationValidation.GetKey(n), n, options, warnings.AsReadOnly());

        bool Keep(SystemNotificationFeatures feature)
        {
            if (capabilities.Supports(feature)) return true;
            warnings.Add(new(feature.ToString(), $"{feature} is unavailable; the optional value was removed."));
            return false;
        }
        bool KeepWindows(WindowsSystemNotificationFeatures feature)
        {
            if (windows.Supports(feature)) return true;
            warnings.Add(new(feature.ToString(), $"{feature} is unavailable; the optional value was removed."));
            return false;
        }
        void RejectDegradation()
        {
            if (!notification.AllowDegradation && warnings.Count > 0) throw new WindowsNotificationContentException(warnings.AsReadOnly());
        }
        string? ImageSource(string source)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) { warnings.Add(new("InvalidImage", "An image source is not an absolute path or URI.")); return null; }
            if (uri.IsFile)
            {
                try
                {
                    // A small header probe catches invalid files without decoding an untrusted large
                    // bitmap. Windows performs final decode; files remain application-owned.
                    using var stream = File.OpenRead(uri.LocalPath);
                    Span<byte> header = stackalloc byte[16];
                    int count = stream.Read(header);
                    bool valid = count >= 8 && (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                        header[0] == 0xff && header[1] == 0xd8 || header[..3].SequenceEqual("GIF"u8) || header[..2].SequenceEqual("BM"u8));
                    if (!valid || stream.Length > 3 * 1024 * 1024) { warnings.Add(new("InvalidImage", "A local image has an invalid header or exceeds the 3 MiB validation limit.")); return null; }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warnings.Add(new("InvalidImage", "A local image could not be read.")); return null; }
            }
            else if (uri.Scheme is "http" or "https") { if (!Keep(SystemNotificationFeatures.RemoteImages)) return null; }
            else if (uri.Scheme is "ms-appx" or "ms-appdata") { if (!Keep(SystemNotificationFeatures.ResourceImages)) return null; }
            else { warnings.Add(new("InvalidImage", "An image uses an unsupported URI scheme.")); return null; }
            return uri.AbsoluteUri;
        }
    }

    /// <summary>Validates Windows NotificationData text, including its required nonempty status.</summary>
    public static void ValidateProgress(SystemNotificationProgress progress)
    {
        SystemNotificationValidation.ValidateProgress(progress);
        SystemNotificationValidation.CheckString(progress.Status, 1024, false);
        SystemNotificationValidation.CheckString(progress.Title, 1024);
        SystemNotificationValidation.CheckString(progress.ValueText, 1024);
    }

    private static bool ValidSystemSound(string value)
    {
        if (value is "Notification.Default" or "Notification.IM" or "Notification.Mail" or "Notification.Reminder" or "Notification.SMS") return true;
        foreach (var prefix in new[] { "Notification.Looping.Alarm", "Notification.Looping.Call" })
            if (value == prefix || value.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(value[prefix.Length..], out int n) && n is >= 2 and <= 10) return true;
        return false;
    }

    /// <summary>Encodes restart-safe application data. The protocol never includes an executable command.</summary>
    public static string EncodeActivation(string id, string arguments, string? actionId, string group = "")
        => "mfn=1&id=" + Uri.EscapeDataString(id) + "&args=" + Uri.EscapeDataString(arguments) + "&group=" + Uri.EscapeDataString(group) + (actionId is null ? "" : "&action=" + Uri.EscapeDataString(actionId));

    /// <summary>Decodes bounded native activation, preserving raw-XML arguments when no framework envelope exists.</summary>
    public static SystemNotificationActivation DecodeActivation(string arguments, IReadOnlyDictionary<string, string> inputs)
    {
        SystemNotificationValidation.CheckString(arguments, 16384);
        if (!arguments.StartsWith("mfn=1&", StringComparison.Ordinal)) return SystemNotificationValidation.CopyActivation(new("", arguments, null, inputs));
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in arguments.Split('&'))
        {
            int separator = part.IndexOf('=');
            if (separator < 1 || !fields.TryAdd(part[..separator], Uri.UnescapeDataString(part[(separator + 1)..])))
                throw new ArgumentException("Malformed notification activation envelope.");
        }
        return SystemNotificationValidation.CopyActivation(new(fields.GetValueOrDefault("id", ""), fields.GetValueOrDefault("args", ""), fields.GetValueOrDefault("action"), inputs) { Group = fields.GetValueOrDefault("group", "") });
    }

    private static string ParseRaw(string xml)
    {
        if (Encoding.UTF8.GetByteCount(xml) > 5000) throw new ArgumentException("Toast payload exceeds 5000 UTF-8 bytes.");
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5000 });
        var doc = XDocument.Load(reader);
        if (doc.Root?.Name != "toast" || doc.Root.Elements("visual").Count() != 1) throw new ArgumentException("Raw payload requires one toast root and visual element.");
        if (doc.Descendants().Count() > 128 || doc.Descendants().Any(e => e.Ancestors().Count() > 12)) throw new ArgumentException("Raw payload exceeds structural limits.");
        return doc.ToString(SaveOptions.DisableFormatting);
    }
}

// Carries degradation diagnostics across the transport boundary without exposing user content.
internal sealed class WindowsNotificationContentException(IReadOnlyList<SystemNotificationWarning> warnings)
    : NotSupportedException("Optional content is unavailable and degradation is disabled.")
{
    internal IReadOnlyList<SystemNotificationWarning> Warnings { get; } = warnings;
}
