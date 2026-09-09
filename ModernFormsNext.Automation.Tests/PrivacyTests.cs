using System.Reflection;
using System.Text.Json;
using ModernFormsNext.Accessibility;
using Xunit;

namespace ModernFormsNext.Automation.Tests;

[Trait("Category", "Privacy")]
public sealed class PrivacyTests
{
    private const string Secret = "AUTOMATION_PRIVATE_MARKER_97_6f893a";

    [Fact]
    public void PasswordSetValueHasNoReadbackLeak()
    {
        using var f = new AutomationFixture();
        var password = f.Add(new TextBox { Name = "password", PasswordCharacter = '*', Text = Secret });
        var before = f.Inspect(password);
        Assert.Equal(AutomationErrorCode.None, before.Error);
        Assert.Null(before.Value!.Value);
        Assert.Null(before.Value.RangeValue);
        Assert.NotEqual(AutomationRedaction.None, before.Value.Redaction);
        NoSecret(before);
        var result = f.Act(password, AccessibleActions.SetValue, AutomationActionValue.FromText(Secret + "updated"));
        Assert.Equal(AutomationActionStatus.Accepted, result.Status);
        Assert.EndsWith("updated", password.Text, StringComparison.Ordinal);
        NoSecret(result); NoSecret(f.Inspect(password)); NoSecret(f.Find(new()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SensitiveAndProtectedCustomPeersSuppressAllTextAndRangeGetters(bool protectedState)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        c.Child.SensitiveGetter = () => !protectedState;
        c.Child.States = protectedState ? AccessibleStates.Protected : AccessibleStates.None;
        int payloadReads = 0;
        c.Child.AutomationId = Secret;
        c.Child.NameGetter = () => { payloadReads++; return Secret; };
        c.Child.ValueGetter = () => { payloadReads++; return Secret; };
        c.Child.RangeGetter = () => { payloadReads++; return new(1, 0, 5, 1, 1, false); };
        var result = f.Find(new());
        Assert.Equal(AutomationErrorCode.None, result.Error);
        Assert.Equal(0, payloadReads);
        NoSecret(result);
        var snapshot = Assert.Single(result.Value, n => n.RuntimeId == c.Child.RuntimeId.ToString());
        Assert.Null(snapshot.Name); Assert.Null(snapshot.AutomationId); Assert.Null(snapshot.Value); Assert.Null(snapshot.RangeValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivacyGetterFailureFailsClosedAndDoesNotEndSession(bool stateGetter)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        if (stateGetter) c.Child.StateGetter = () => throw new Exception(Secret);
        else c.Child.SensitiveGetter = () => throw new Exception(Secret);
        c.Child.ValueGetter = () => throw new InvalidOperationException("Payload should never be read");
        var result = f.Find(new());
        Assert.Equal(AutomationErrorCode.GetterFault, result.Error);
        Assert.Contains(result.Value, n => (n.Redaction & AutomationRedaction.PrivacyUnknown) != 0);
        Assert.DoesNotContain(result.Issues, i => i.Property == AutomationProperty.Value);
        Assert.False(f.Session.IsStopped);
        NoSecret(result);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("value")]
    [InlineData("child")]
    [InlineData("action")]
    public void ExceptionMessagesAreNotExported(string field)
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        switch (field)
        {
            case "name": c.Child.NameGetter = () => throw new Exception(Secret); break;
            case "value": c.Child.ValueGetter = () => throw new Exception(Secret); break;
            case "child": c.Child.CountGetter = () => throw new Exception(Secret); break;
            case "action": c.Child.Action = (_, _) => throw new Exception(Secret); break;
        }
        if (field == "action")
        {
            var result = f.Session.PerformActionAsync(f.Root.RootId, f.Handle(c.Child), AccessibleActions.Invoke).Completed();
            Assert.Equal(AutomationErrorCode.ApplicationError, result.Error); NoSecret(result);
        }
        else
        {
            var result = f.Find(new());
            Assert.Equal(AutomationErrorCode.GetterFault, result.Error); NoSecret(result);
        }
        Assert.Empty(f.Host.Dispatcher.UnhandledExceptions);
    }

    [Fact]
    public void SensitiveAncestorRedactsUnmarkedCustomDescendants()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        c.Child.SensitiveGetter = () => true;
        c.Child.Add(new ScriptPeer { Name = Secret, Value = Secret, AutomationId = Secret });
        NoSecret(f.Find(new()));
    }

    [Fact]
    public void EditableTextNeverFallsBackIntoNameQuery()
    {
        using var f = new AutomationFixture(); var text = f.Add(new TextBox { Name = "field", Text = Secret });
        Assert.Empty(f.Find(new() { Name = Secret }).Value);
        Assert.Equal("field", f.Inspect(text).Value!.Name);
        // This is explicitly non-sensitive editable value, which remains valid semantic output.
        Assert.True(f.Inspect(text).Value!.Value == Secret);
    }

    [Fact]
    public void HostileCommandParameterToStringIsNeverCalled()
    {
        using var f = new AutomationFixture(); var parameter = new HostileParameter();
        int called = 0;
        var b = f.Add(new Button { CommandParameter = parameter, Command = new DelegateCommand(p => { Assert.Same(parameter, p); called++; }) });
        var result = f.Act(b, AccessibleActions.Invoke);
        Assert.Equal(AutomationActionStatus.Accepted, result.Status);
        Assert.Equal(1, called); Assert.Equal(0, parameter.StringCalls);
        NoSecret(result); NoSecret(f.Inspect(b));
    }

    [Fact]
    public void ActionPayloadDiagnosticStringAndResultsOmitSecret()
    {
        using var f = new AutomationFixture(); var value = AutomationActionValue.FromText(Secret);
        Assert.DoesNotContain(Secret, value.ToString());
        NoSecret(f.Act(f.Add(new Button()), AccessibleActions.Invoke, value));
        Assert.True(typeof(AutomationActionValue).IsSealed);
        Assert.DoesNotContain(typeof(AutomationSession).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == typeof(AutomationSession)).SelectMany(m => m.GetParameters()), p => p.ParameterType == typeof(object));
    }

    [Fact]
    public void OversizedPayloadIsOmittedWithExplicitLimit()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl());
        c.Child.Name = new string('x', 5000);
        var result = f.Find(new());
        Assert.True(result.Truncated); Assert.Equal(AutomationErrorCode.LimitExceeded, result.Error);
        Assert.Null(Assert.Single(result.Value, n => n.RuntimeId == c.Child.RuntimeId.ToString()).Name);
        Assert.Equal(AutomationErrorCode.InvalidArgument, f.Act(f.Add(new TextBox()), AccessibleActions.SetValue, AutomationActionValue.FromText(c.Child.Name)).Error);
    }

    [Fact]
    public void CustomPasswordPeerCannotNegateKnownOwnerPrivacy()
    {
        using var f = new AutomationFixture(); var c = f.Add(new UnsafePassword { PasswordCharacter = '*', Text = Secret });
        var snapshot = f.Inspect(c);
        Assert.NotEqual(AutomationRedaction.None, snapshot.Value!.Redaction);
        NoSecret(snapshot);
    }

    [Fact]
    public void PrivacyEscalationDuringGetterDiscardsAlreadyCapturedPayload()
    {
        using var f = new AutomationFixture(); var c = f.Add(new SemanticControl()); bool sensitive = false;
        c.Child.SensitiveGetter = () => sensitive;
        c.Child.NameGetter = () => { sensitive = true; return Secret; };
        c.Child.Value = Secret;
        var result = f.Find(new());
        NoSecret(result);
        Assert.NotEqual(AutomationRedaction.None, Assert.Single(result.Value, n => n.RuntimeId == c.Child.RuntimeId.ToString()).Redaction);
    }

    private static void NoSecret<T>(T value) => Assert.DoesNotContain(Secret, JsonSerializer.Serialize(value), StringComparison.Ordinal);

    [Fact]
    public void PrivacyFailureStillRedactsWhenDiagnosticBudgetIsFull()
    {
        using var f = new AutomationFixture(new() { MaxNodes = 4 });
        var c = f.Add(new SemanticControl());
        int stateReads = 0;
        c.Child.StateGetter = () => ++stateReads >= 3 ? throw new Exception(Secret) : AccessibleStates.None;
        c.Child.AutomationId = Secret;
        c.Child.NameGetter = () => throw new Exception(Secret);
        c.Child.ValueGetter = () => throw new Exception(Secret);
        c.Child.RangeGetter = () => throw new Exception(Secret);
        c.Child.ActionsGetter = () => throw new Exception(Secret);
        var result = f.Session.InspectAsync(f.Root.RootId, f.Handle(c.Child)).Completed();
        Assert.Equal(4, result.Issues.Length);
        Assert.True(result.Truncated);
        Assert.True((result.Value!.Redaction & AutomationRedaction.PrivacyUnknown) != 0);
        Assert.False(JsonSerializer.Serialize(result).Contains(Secret, StringComparison.Ordinal));
    }
    private sealed class HostileParameter
    {
        internal int StringCalls;
        public override string ToString() { StringCalls++; throw new Exception(Secret); }
    }
    private sealed class UnsafePassword : TextBox
    {
        protected override AccessibleObject CreateAccessibilityInstance() => new Peer(this);
        private sealed class Peer(Control owner) : ControlAccessibleObject(owner)
        {
            public override bool IsSensitive => false;
            public override AccessibleStates State => AccessibleStates.None;
            public override string? Value { get => Secret; set { } }
            public override string? Name { get => Secret; set { } }
        }
    }
}
