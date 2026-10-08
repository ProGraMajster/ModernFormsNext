using System.Xml.Linq;
using ModernFormsNext.WindowKit.Backend.Notifications;
using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Windows.Notifications;
using Xunit;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class SystemNotificationContentTests
{
    private static readonly SystemNotificationCapabilities All = new("test", Enum.GetValues<SystemNotificationFeatures>().Aggregate((a, b) => a | b))
    { Platform = new WindowsSystemNotificationCapabilities(Enum.GetValues<WindowsSystemNotificationFeatures>().Aggregate((a, b) => a | b)),
      Limits = new() { TextBlocks = 3, Images = 3, Actions = 5, Inputs = 5, Choices = 5 } };

    [Theory]
    [InlineData(6, 1, 7601, false, false, WindowsNotificationBackendKind.Shell)]
    [InlineData(6, 2, 9200, false, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(6, 3, 9600, false, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 10240, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 10586, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 14393, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 15063, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 16299, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 17134, true, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 17763, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 19045, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 22000, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 22621, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 22631, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 26100, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 26200, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 28000, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 26300, true, true, WindowsNotificationBackendKind.AppSdk)]
    [InlineData(10, 0, 26300, false, true, WindowsNotificationBackendKind.Classic)]
    [InlineData(10, 0, 26300, false, false, WindowsNotificationBackendKind.Shell)]
    public void SelectorUsesActualAvailability(int major, int minor, int build, bool modern, bool classic, WindowsNotificationBackendKind expected)
        => Assert.Equal(expected, WindowsNotificationBackendSelector.Select(new(new(major, minor, build), modern, classic, true, false), new()));

    [Fact]
    public void ElevatedAndExplicitPreferenceDoNotBypassGates()
    {
        var elevated = new WindowsNotificationEnvironment(new(10, 0, 26300), true, true, true, true);
        Assert.Equal(WindowsNotificationBackendKind.Unavailable, WindowsNotificationBackendSelector.Select(elevated, new() { AllowShellFallback = false }));
        Assert.Equal(WindowsNotificationBackendKind.Shell, WindowsNotificationBackendSelector.Select(elevated, new()));
        Assert.Equal(WindowsNotificationBackendKind.Unavailable, WindowsNotificationBackendSelector.Select(elevated, new() { Backend = WindowsNotificationBackendKind.AppSdk }));
    }

    [Theory]
    [InlineData(14393, false, false)]
    [InlineData(15063, true, false)]
    [InlineData(26300, true, false)]
    [InlineData(28000, false, false)]
    public void NewBuildNeverInventsUnprobedFeatures(int build, bool api, bool urgent)
    {
        var f = WindowsNotificationBackendSelector.GetToastCapabilities(new(10, 0, build), false, api, true, urgent, false, false, true, false);
        Assert.Equal(build >= 15063 && api, f.Supports(SystemNotificationFeatures.LiveUpdates));
        Assert.False(f.GetPlatform<WindowsSystemNotificationCapabilities>()!.Supports(WindowsSystemNotificationFeatures.UrgentScenario));
        Assert.False(f.GetPlatform<WindowsSystemNotificationCapabilities>()!.Supports(WindowsSystemNotificationFeatures.ButtonStyles));
    }

    [Fact]
    public void XmlEscapesContentAndActivationRoundTripsUnicode()
    {
        const string payload = "a=&'<>😀 Zażółć";
        var notification = new SystemNotification { Id = "video42", Title = payload, Message = "Message", ActivationData = payload, Actions = [new("Open & folder", payload)] };
        var content = WindowsToastContent.Create(notification, All);
        var doc = XDocument.Parse(content.Xml);
        Assert.Equal(payload, doc.Descendants("text").First().Value);
        var activation = WindowsToastContent.DecodeActivation(doc.Root!.Attribute("launch")!.Value, new Dictionary<string, string>());
        Assert.Equal(payload, activation.ActivationData);
        Assert.Null(activation.ActionId);
        var action = WindowsToastContent.DecodeActivation(doc.Descendants("action").Single().Attribute("arguments")!.Value, new Dictionary<string, string>());
        Assert.Equal(payload, action.ActionId);
    }

    [Fact]
    public void RichXmlContainsInputsProgressImagesAndHeader()
    {
        var n = new SystemNotification
        {
            Title = "Download", Message = "Video", Timestamp = DateTimeOffset.UnixEpoch, Progress = new() { Value = .76, Status = "Downloading" },
            Images = [new("https://example.test/hero.png", SystemNotificationImageRole.Thumbnail, "Thumbnail"), new("ms-appx:///logo.png", SystemNotificationImageRole.Portrait)],
            Inputs = [new("reply") { Placeholder = "Reply" }, new("choice") { Choices = [new("a", "One"), new("b", "Two")], DefaultValue = "b" }],
            Actions = [new("Send", "send") { PlatformOptions = new(new WindowsNotificationActionOptions { InputId = "reply", Style = WindowsNotificationButtonStyle.Success }) }],
            PlatformOptions = new(new WindowsSystemNotificationOptions { Header = new("downloads", "Downloads", "header"), Attribution = "Source", Silent = true })
        };
        var doc = XDocument.Parse(WindowsToastContent.Create(n, All).Xml);
        Assert.Equal("{progressValue}", doc.Descendants("progress").Single().Attribute("value")!.Value);
        Assert.Equal("circle", doc.Descendants("image").Last().Attribute("hint-crop")!.Value);
        Assert.Equal("b", doc.Descendants("input").Last().Attribute("defaultInput")!.Value);
        Assert.Equal("true", doc.Root!.Attribute("useButtonStyle")!.Value);
        Assert.Equal("Success", doc.Descendants("action").Single().Attribute("hint-buttonStyle")!.Value);
        Assert.Single(doc.Descendants("header"));
        Assert.Equal("true", doc.Descendants("audio").Single().Attribute("silent")!.Value);
    }

    [Fact]
    public void InvalidImagesAreReportedAndStrictModeRejects()
    {
        var n = new SystemNotification { Title = "Keep basic message", Images = [new("file:///Z:/does-not-exist.png"), new("javascript:invalid", SystemNotificationImageRole.Thumbnail)] };
        var content = WindowsToastContent.Create(n, All);
        Assert.Empty(XDocument.Parse(content.Xml).Descendants("image"));
        Assert.Equal(2, content.Warnings.Count);
        Assert.ThrowsAny<NotSupportedException>(() => WindowsToastContent.Create(n with { AllowDegradation = false }, All));
    }

    [Theory]
    [InlineData("file:///C:/sound.mp3")]
    [InlineData("https://example.test/sound.mp3")]
    [InlineData("ms-appdata:///local/sound.mp3")]
    [InlineData("ms-winsoundevent:Notification.Unknown")]
    public void UnsupportedAudioIsRejected(string source)
        => Assert.Throws<ArgumentException>(() => WindowsToastContent.Create(new() { Title = "Audio", PlatformOptions = new(new WindowsSystemNotificationOptions { Audio = source }) }, All));

    [Fact]
    public void UnsupportedUrgentScenarioDegradesWithWarning()
    {
        var content = WindowsToastContent.Create(new() { Title = "Message", PlatformOptions = new(new WindowsSystemNotificationOptions { Scenario = WindowsNotificationScenario.Urgent }) },
            All with { Platform = new WindowsSystemNotificationCapabilities(((WindowsSystemNotificationCapabilities)All.Platform!).Features & ~WindowsSystemNotificationFeatures.UrgentScenario) });
        Assert.Null(XDocument.Parse(content.Xml).Root!.Attribute("scenario"));
        Assert.Contains(content.Warnings, w => w.Code == "UrgentScenario");
    }

    [Fact]
    public void RawXmlRejectsEntitiesWrongRootAndExcessiveDepth()
    {
        foreach (var xml in new[] { "<!DOCTYPE toast [<!ENTITY x 'injected'>]><toast><visual>&x;</visual></toast>", "<wrong><visual/></wrong>",
            "<toast><visual>" + string.Concat(Enumerable.Repeat("<group>", 14)) + string.Concat(Enumerable.Repeat("</group>", 14)) + "</visual></toast>" })
            Assert.ThrowsAny<Exception>(() => WindowsToastContent.Create(new() { Title = "Raw", PlatformOptions = new(new WindowsSystemNotificationOptions { RawXml = xml }) }, All));
    }

    [Fact]
    public void RawXmlRequiresDeclaredCapabilitiesAndLegacyDoesNotInventAdaptiveContent()
    {
        var raw = new SystemNotification { Title = "Raw", PlatformOptions = new(new WindowsSystemNotificationOptions { RawXml = "<toast><visual><binding template='ToastGeneric'><text>Raw</text></binding></visual></toast>", RequiredFeatures = WindowsSystemNotificationFeatures.UrgentScenario }) };
        Assert.Throws<NotSupportedException>(() => WindowsToastContent.Create(raw, All with { Platform = null, Features = SystemNotificationFeatures.Basic }));
        var legacy = new SystemNotificationCapabilities("legacy", SystemNotificationFeatures.Basic | SystemNotificationFeatures.Images);
        var content = WindowsToastContent.Create(new() { Title = "Legacy", Message = "Body", Actions = [new("Action", "action")], Progress = new() { Status = "Progress" } }, legacy, true);
        var doc = XDocument.Parse(content.Xml);
        Assert.Equal("ToastText02", doc.Descendants("binding").Single().Attribute("template")!.Value);
        Assert.Empty(doc.Descendants("action"));
        Assert.Empty(doc.Descendants("progress"));
        Assert.NotEmpty(content.Warnings);
    }

    [Fact]
    public void LongLogicalIdentityAndReservedNameSurviveNativeHistoryRoundTrip()
    {
        var notification = new SystemNotification { Id = "download/" + new string('x', 200), Group = "mfn.default", Title = "Download", ActivationData = "open" };
        var content = WindowsToastContent.Create(notification, All);
        string tag = WindowsToastContent.ToNativeTag(notification.Id), group = WindowsToastContent.ToNativeGroup(notification.Group);
        Assert.Equal(16, tag.Length);
        Assert.Equal(16, group.Length);
        Assert.NotEqual(group, WindowsToastContent.ToNativeGroup(""));
        Assert.Equal(tag, WindowsToastContent.ToNativeTag(notification.Id));
        var entry = WindowsToastContent.ReadHistory(content.Xml, tag, group, Guid.NewGuid());
        Assert.Equal(new SystemNotificationKey(notification.Id, notification.Group), entry.Key);
        var activation = WindowsToastContent.DecodeActivation(XDocument.Parse(content.Xml).Root!.Attribute("launch")!.Value, new Dictionary<string, string>());
        Assert.Equal(notification.Group, activation.Group);
        Assert.Equal("open", activation.ActivationData);
    }

    [Fact]
    public void ForeignRawHistoryRetainsExactNativeReferenceWithoutInventingLogicalId()
    {
        var owner = Guid.NewGuid();
        var entry = WindowsToastContent.ReadHistory("<toast launch='custom:open'><visual/></toast>", "native-tag", "native-group", owner);
        Assert.Null(entry.Key);
        Assert.Equal(new WindowsSystemNotificationReference(owner, "native-tag", "native-group"), entry.Reference);
        var mismatch = WindowsToastContent.ReadHistory(WindowsToastContent.Create(new() { Id = "one", Title = "One" }, All).Xml, WindowsToastContent.ToNativeTag("two"), WindowsToastContent.ToNativeGroup(""), owner);
        Assert.Null(mismatch.Key);
    }

    [Fact]
    public void WindowsImageOptionsOverrideSemanticRoleAndRetainQueryAndCircleCrop()
    {
        var content = WindowsToastContent.Create(new() { Title = "Image", Images = [new("https://example.test/image.png", SystemNotificationImageRole.Content)
            { PlatformOptions = new(new WindowsSystemNotificationImageOptions { Placement = WindowsNotificationImagePlacement.Avatar, AddImageQuery = true }) }] }, All);
        var image = XDocument.Parse(content.Xml).Descendants("image").Single();
        Assert.Equal("appLogoOverride", image.Attribute("placement")!.Value);
        Assert.Equal("circle", image.Attribute("hint-crop")!.Value);
        Assert.Equal("true", image.Attribute("addImageQuery")!.Value);
    }

    [Fact]
    public void NativeSystemActionsAndProtocolRemainAvailableThroughWindowsExtensions()
    {
        var content = WindowsToastContent.Create(new() { Title = "Reminder",
            PlatformOptions = new(new WindowsSystemNotificationOptions { Scenario = WindowsNotificationScenario.Reminder }),
            Inputs = [new("minutes") { Choices = [new("5", "Five minutes")] }],
            Actions = [new("Snooze", "") { PlatformOptions = new(new WindowsNotificationActionOptions { ActivationType = WindowsNotificationActivationType.Snooze, InputId = "minutes" }) },
                new("Dismiss", "") { PlatformOptions = new(new WindowsNotificationActionOptions { ActivationType = WindowsNotificationActivationType.Dismiss }) },
                new("Open", "data") { ActivationType = SystemNotificationActivationType.OpenUri, TargetUri = new("https://example.test/open"),
                    PlatformOptions = new(new WindowsNotificationActionOptions { TargetApplicationPfn = "example", ContextMenu = true, ToolTip = "Open target" }) }] }, All);
        var actions = XDocument.Parse(content.Xml).Descendants("action").ToArray();
        Assert.Equal("system", actions[0].Attribute("activationType")!.Value);
        Assert.Equal("snooze", actions[0].Attribute("arguments")!.Value);
        Assert.Equal("dismiss", actions[1].Attribute("arguments")!.Value);
        Assert.Equal("protocol", actions[2].Attribute("activationType")!.Value);
        Assert.Equal("https://example.test/open", actions[2].Attribute("arguments")!.Value);
        Assert.Equal("example", actions[2].Attribute("activationOptions-protocolActivationTargetApplicationPfn")!.Value);
        Assert.Equal("contextMenu", actions[2].Attribute("placement")!.Value);
    }

    [Theory]
    [InlineData("Enabled", SystemNotificationAvailability.Ready)]
    [InlineData("DisabledForApplication", SystemNotificationAvailability.DisabledByUser)]
    [InlineData("DisabledForUser", SystemNotificationAvailability.DisabledByUser)]
    [InlineData("DisabledByGroupPolicy", SystemNotificationAvailability.Restricted)]
    [InlineData("DisabledByManifest", SystemNotificationAvailability.Restricted)]
    [InlineData("Unsupported", SystemNotificationAvailability.Unavailable)]
    [InlineData("FutureSetting", SystemNotificationAvailability.Unknown)]
    public void WindowsSettingsNeverPretendToRequestRuntimeConsent(string setting, SystemNotificationAvailability expected)
    {
        var result = WindowsNotificationAccess.FromSetting(setting);
        Assert.Equal(expected, result.Availability);
        Assert.False(result.CanRequestPermission);
    }

    [Fact]
    public void NativeTextAndInputBoundsDoNotLeakIntoCommonValidation()
    {
        var notification = new SystemNotification { Title = "Long input ID", Inputs = [new(new string('i', 65))] };
        Assert.NotNull(SystemNotificationValidation.Normalize(notification, All, new List<SystemNotificationWarning>()));
        Assert.Throws<ArgumentException>(() => WindowsToastContent.Create(notification, All));
        Assert.Throws<ArgumentException>(() => WindowsToastContent.ValidateProgress(new() { Value = .2 }));
        Assert.ThrowsAny<Exception>(() => WindowsToastContent.Create(new() { Title = "Invalid XML \u0001" }, All));
    }

    [Fact]
    public void RawXmlCanRequireSemanticFeaturesSeparatelyFromWindowsExtensions()
    {
        var raw = new SystemNotification { Title = "Raw", PlatformOptions = new(new WindowsSystemNotificationOptions
        { RawXml = "<toast><visual><binding template='ToastGeneric'><text>Raw</text></binding></visual></toast>", RequiredCommonFeatures = SystemNotificationFeatures.Progress }) };
        Assert.Throws<NotSupportedException>(() => WindowsToastContent.Create(raw, All with { Features = All.Features & ~SystemNotificationFeatures.Progress }));
        Assert.NotNull(WindowsToastContent.Create(raw, All));
    }
    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("Zażółć/😀")]
    public void EveryValidNonemptyLogicalGroupMapsWithoutLosingIdentity(string group)
    {
        var notification = new SystemNotification { Id = "identity/😀", Group = group, Title = "Group" };
        var content = WindowsToastContent.Create(notification, All);
        var nativeGroup = WindowsToastContent.ToNativeGroup(group);
        Assert.Equal(16, nativeGroup.Length);
        Assert.NotEqual(WindowsToastContent.ToNativeGroup(""), nativeGroup);
        Assert.Equal(nativeGroup, WindowsToastContent.ToNativeGroup(group));
        Assert.Equal(new(notification.Id, group), WindowsToastContent.ReadHistory(content.Xml,
            WindowsToastContent.ToNativeTag(notification.Id), nativeGroup, Guid.NewGuid()).Key);
    }

    [Fact]
    public void NativeIdentityMappingAcceptsTheFullCommonBoundAndKeepsOrdinalDistinctions()
    {
        var id = new string('x', 4096);
        var group = new string('g', 4096);
        Assert.Equal(16, WindowsToastContent.ToNativeTag(id).Length);
        Assert.Equal(16, WindowsToastContent.ToNativeGroup(group).Length);
        Assert.NotEqual(WindowsToastContent.ToNativeTag("A"), WindowsToastContent.ToNativeTag("a"));
        Assert.NotEqual(WindowsToastContent.ToNativeTag("é"), WindowsToastContent.ToNativeTag("e\u0301"));
        // Hash acceptance is separate from Windows' total native XML payload limit.
        Assert.Throws<ArgumentException>(() => WindowsToastContent.Create(new() { Id = id, Group = group, Title = "Large" }, All));
    }

    [Fact]
    public void LegacyBodyUsesTheSameLogicalLaunchEnvelopeAndIgnoresForeignOptions()
    {
        var notification = new SystemNotification { Id = "legacy-id", Group = "conversation/😀", Title = "Legacy", ActivationData = "body-data",
            PlatformOptions = new(new ForeignOptions()) };
        var capabilities = WindowsNotificationBackendSelector.GetToastCapabilities(new(6, 2), false, false, true, false, false, false, false, false);
        var content = WindowsToastContent.Create(notification, capabilities, legacyTemplate: true);
        var launch = XDocument.Parse(content.Xml).Root!.Attribute("launch")!.Value;
        var response = WindowsToastContent.DecodeActivation(launch, new Dictionary<string, string>());
        Assert.Equal(notification.Id, response.NotificationId);
        Assert.Equal(notification.Group, response.Group);
        Assert.Equal(notification.ActivationData, response.ActivationData);
        Assert.Null(response.ActionId);
        Assert.Empty(content.Warnings);
    }

    private sealed record ForeignOptions : SystemNotificationPlatformOptions;

}
