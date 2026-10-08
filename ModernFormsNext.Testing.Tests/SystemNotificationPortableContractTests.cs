using ModernFormsNext.Notifications;
using ModernFormsNext.WindowKit.Backend.Notifications;
using Xunit;

#pragma warning disable xUnit1031 // Deterministic host operations complete inline on its owning UI thread.
namespace ModernFormsNext.Testing.Tests;

public sealed class SystemNotificationPortableContractTests
{
    private const SystemNotificationFeatures Rich = SystemNotificationFeatures.Basic | SystemNotificationFeatures.Images |
        SystemNotificationFeatures.Actions | SystemNotificationFeatures.Inputs | SystemNotificationFeatures.Progress |
        SystemNotificationFeatures.LiveUpdates | SystemNotificationFeatures.IndeterminateProgress | SystemNotificationFeatures.Replacement |
        SystemNotificationFeatures.Grouping | SystemNotificationFeatures.History | SystemNotificationFeatures.SenderMetadata;

    [Theory]
    [InlineData("Modern Windows", Rich, true, true, true)]
    [InlineData("Legacy Windows", SystemNotificationFeatures.Basic, false, false, false)]
    [InlineData("Future Android", Rich & ~SystemNotificationFeatures.History, true, true, true)]
    [InlineData("Future iOS", Rich & ~(SystemNotificationFeatures.Progress | SystemNotificationFeatures.LiveUpdates | SystemNotificationFeatures.SelectionInput), true, false, false)]
    [InlineData("Future macOS", Rich & ~(SystemNotificationFeatures.Progress | SystemNotificationFeatures.LiveUpdates | SystemNotificationFeatures.SelectionInput), true, false, false)]
    [InlineData("Future Linux portal", SystemNotificationFeatures.Basic | SystemNotificationFeatures.Actions | SystemNotificationFeatures.Replacement, true, false, false)]
    [InlineData("Linux minimal server", SystemNotificationFeatures.Basic, false, false, false)]
    public void CapabilityCombinationsDescribeIntentWithoutInventingPlatformProviders(string profile, SystemNotificationFeatures features, bool actions, bool progress, bool choice)
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.SetCapabilities(new SystemNotificationCapabilities(profile, features));
        var result = service.ShowAsync(new() { Id = "logical notification identity longer than sixteen characters", Group = "conversation/group/also/long",
            Title = "Update", Images = [new("resource:portrait", SystemNotificationImageRole.Portrait)],
            Sender = new("sender-1", "Sender"), Progress = new() { Value = .5 },
            Actions = [new("Open", "open")], Inputs = [new("reply"), new("choice") { Choices = [new("yes", "Yes")] }] }).Result;
        Assert.True(result.IsAccepted);
        var stored = service.Notifications[result.Key!];
        Assert.Equal(actions, stored.Actions.Count > 0);
        Assert.Equal(progress, stored.Progress is not null);
        Assert.Equal(choice, stored.Inputs.Any(input => input.Id == "choice"));
        Assert.Equal("logical notification identity longer than sixteen characters", result.Key!.Id);
        Assert.Equal(features, service.Capabilities.Features);
    }

    [Theory]
    [InlineData(SystemNotificationAvailability.PermissionRequired, SystemNotificationStatus.PermissionRequired)]
    [InlineData(SystemNotificationAvailability.PermissionDenied, SystemNotificationStatus.PermissionDenied)]
    [InlineData(SystemNotificationAvailability.DisabledByUser, SystemNotificationStatus.Disabled)]
    [InlineData(SystemNotificationAvailability.Restricted, SystemNotificationStatus.Restricted)]
    [InlineData(SystemNotificationAvailability.Unavailable, SystemNotificationStatus.Unavailable)]
    public void AccessBlocksSubmissionWithoutChangingCapabilitiesOrPrompting(SystemNotificationAvailability availability, SystemNotificationStatus expected)
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.SetCapabilities(Rich);
        service.SetAccess(new(availability, true));
        Assert.True(SystemNotifications.IsSupported);
        Assert.Equal(availability, SystemNotifications.GetStatusAsync().Result.Availability);
        Assert.Equal(expected, service.ShowAsync(new() { Title = "Message" }).Result.Status);
        Assert.Equal(expected, service.UpdateAsync(new("existing"), new() { Value = .2 }).Result.Status);
        Assert.Empty(service.Notifications);
        Assert.Equal(0, service.PermissionRequests);
        Assert.Equal(Rich, service.Capabilities.Features);
        service.SetPermissionResponse(new(SystemNotificationAvailability.Ready) { AllowedPresentation = SystemNotificationPresentation.Sound });
        var result = SystemNotifications.RequestPermissionAsync().Result;
        Assert.Equal(SystemNotificationAvailability.Ready, result.Availability);
        Assert.Equal(SystemNotificationPresentation.Sound, result.AllowedPresentation);
        Assert.Equal(1, service.PermissionRequests);
        Assert.True(service.ShowAsync(new() { Title = "Authorized" }).Result.IsAccepted);
    }

    [Fact]
    public void PerBackendLimitsDegradeDependenciesAndStrictModeRejects()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var notification = new SystemNotification { Title = "Title", Message = "Body", Text = [new("Three"), new("Four")],
            Inputs = [new("first"), new("second")], Actions = [new("Reply", "reply") { InputIds = ["second"] }, new("Open", "open")] };
        service.SetCapabilities(new SystemNotificationCapabilities("unbounded-native-contract", Rich));
        Assert.True(service.ShowAsync(notification).Result.IsAccepted);
        Assert.Equal(2, service.Notifications[new(notification.Id)].Text.Count);
        service.SetCapabilities(new SystemNotificationCapabilities("limited", Rich) { Limits = new() { TextBlocks = 2, Inputs = 1, Actions = 1 } });
        var result = service.ShowAsync(notification).Result;
        Assert.True(result.IsAccepted);
        Assert.Empty(service.Notifications[result.Key!].Text);
        Assert.Equal("Open", Assert.Single(service.Notifications[result.Key!].Actions).Title);
        Assert.Contains(result.Warnings!, warning => warning.Code == "ActionInputs");
        Assert.Equal(SystemNotificationStatus.Unsupported, service.ShowAsync(notification with { AllowDegradation = false }).Result.Status);
        Assert.Equal(2, notification.Inputs.Count);
    }

    [Fact]
    public void OptionsCoexistAndSnapshotNestedStateWithoutKnowingPlatformAssemblies()
    {
        using var host = ModernFormsTestHost.Create();
        var values = new List<string> { "channel" };
        var options = new SystemNotificationOptions(new FirstOptions(values), new SecondOptions("category"));
        values[0] = "mutated";
        Assert.Equal("channel", options.Get<FirstOptions>()!.Values[0]);
        Assert.Equal("category", options.Get<SecondOptions>()!.Category);
        var updated = options.With(new SecondOptions("other"));
        Assert.Equal("category", options.Get<SecondOptions>()!.Category);
        Assert.Equal("other", updated.Get<SecondOptions>()!.Category);
        Assert.Throws<ArgumentException>(() => new SystemNotificationOptions(new SecondOptions("a"), new SecondOptions("b")));
        var inputIds = new List<string> { "reply" };
        var result = host.Services.SystemNotifications.ShowAsync(new() { Title = "Options", PlatformOptions = options,
            Inputs = [new("reply")], Actions = [new("Send", "payload") { Id = "send", InputIds = inputIds, PlatformOptions = updated }] }).Result;
        inputIds.Clear();
        var stored = host.Services.SystemNotifications.Notifications[result.Key!];
        Assert.Equal("reply", Assert.Single(stored.Actions[0].InputIds));
        Assert.Equal("channel", stored.PlatformOptions.Get<FirstOptions>()!.Values[0]);
        Assert.Equal("other", stored.Actions[0].PlatformOptions.Get<SecondOptions>()!.Category);
    }

    [Fact]
    public void ActivationRetainsLogicalGroupAndHistoryReferencesCannotCrossServices()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        var key = service.ShowAsync(new() { Id = "message", Group = "conversation", Title = "Message", ActivationData = "payload" }).Result.Key!;
        SystemNotificationActivation? response = null;
        service.Activated += (_, value) => response = value;
        service.Activate(key, userInput: new Dictionary<string, string> { ["reply"] = "Hello" });
        host.Dispatcher.Drain();
        Assert.Equal("conversation", response!.Group);
        Assert.Equal("payload", response.ActivationData);
        var entry = Assert.Single(service.GetHistoryAsync().Result);
        Assert.Equal(key, entry.Key);
        Assert.Equal(SystemNotificationStatus.Invalid, service.DismissHistoryAsync(new ForeignReference()).Result.Status);
        Assert.True(service.DismissHistoryAsync(entry.Reference).Result.IsAccepted);
        Assert.Empty(service.Notifications);
    }

    [Fact]
    public void CommonTextIsUnicodeRatherThanXmlAndProgressHasNoRequiredNativeLabel()
    {
        var warnings = new List<SystemNotificationWarning>();
        var normalized = SystemNotificationValidation.Normalize(new() { Title = "Unicode control \u0001", Progress = new() { Value = .5 } }, new("portable", Rich), warnings);
        Assert.Empty(warnings);
        Assert.Equal("", normalized.Progress!.Status);
        Assert.Null(typeof(SystemNotificationProgress).GetProperty("SequenceNumber"));
        Assert.DoesNotContain(Enum.GetNames<SystemNotificationFeatures>(), name => name is "HeroImages" or "RawXml" or "Headers");
    }


    [Fact]
    public void RemovalDoesNotRequireInventedHistorySupport()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.SetCapabilities(SystemNotificationFeatures.Basic | SystemNotificationFeatures.Removal);
        var key = service.ShowAsync(new() { Title = "Native ID only" }).Result.Key!;
        Assert.Empty(service.GetHistoryAsync().Result);
        Assert.True(service.DismissAsync(key).Result.IsAccepted);
        Assert.Empty(service.Notifications);
        Assert.Equal(SystemNotificationStatus.Unsupported, service.ClearAsync().Result.Status);
    }

    [Fact]
    public void ZeroChoiceLimitCannotSilentlyTurnASelectionIntoFreeText()
    {
        using var host = ModernFormsTestHost.Create();
        var service = host.Services.SystemNotifications;
        service.SetCapabilities(new SystemNotificationCapabilities("limited", Rich) { Limits = new() { Choices = 0 } });
        var shown = service.ShowAsync(new() { Title = "Choose", Inputs = [new("pick") { Choices = [new("one", "One")] }],
            Actions = [new("Send", "send") { InputIds = ["pick"] }] }).Result;
        Assert.True(shown.IsAccepted);
        Assert.Empty(service.Notifications[shown.Key!].Inputs);
        Assert.Empty(service.Notifications[shown.Key!].Actions);
    }

    [Fact]
    public void InvalidOptionSnapshotsFailBeforeTheyCanCrossAProviderBoundary()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemNotificationOptions((SystemNotificationPlatformOptions[])null!));
        Assert.Throws<ArgumentNullException>(() => new SystemNotificationOptions([null!]));
        Assert.Throws<ArgumentException>(() => new SystemNotificationOptions(new WrongTypeOptions()));
        Assert.Throws<ArgumentException>(() => new SystemNotificationOptions(new NullSnapshotOptions()));
    }

    private sealed record WrongTypeOptions : SystemNotificationPlatformOptions
    {
        public override SystemNotificationPlatformOptions Snapshot() => new SecondOptions("wrong type");
    }
    private sealed record NullSnapshotOptions : SystemNotificationPlatformOptions
    {
        public override SystemNotificationPlatformOptions Snapshot() => null!;
    }

    private sealed record FirstOptions(IReadOnlyList<string> Values) : SystemNotificationPlatformOptions
    {
        public override SystemNotificationPlatformOptions Snapshot() => this with { Values = Array.AsReadOnly(Values.ToArray()) };
    }
    private sealed record SecondOptions(string Category) : SystemNotificationPlatformOptions;
    private sealed record ForeignReference : SystemNotificationReference;
}
