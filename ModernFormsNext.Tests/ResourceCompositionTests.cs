using System.Runtime.CompilerServices;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class ResourceCompositionTests
{
    private static readonly ResourceProperty<Probe, int> ValueProperty = new(nameof(Probe.Value),
        static control => control.Value, static (control, value) => control.Value = value);

    [Fact]
    public void LocalThenLastMergedWinsIncludingNull()
    {
        var first = new ResourceDictionary { ["key"] = 1 };
        var second = new ResourceDictionary { ["key"] = 2 };
        var root = new ResourceDictionary();
        root.MergedDictionaries.Add(first);
        root.MergedDictionaries.Add(second);
        Assert.Equal(2, root["key"]);
        Assert.True(root.ContainsKey("key"));
        Assert.Empty(root); // local enumeration is deliberately unchanged
        root["key"] = null;
        Assert.Null(root["key"]);
        root.Remove("key");
        Assert.Equal(2, root["key"]);
        root.MergedDictionaries.Remove(second);
        Assert.Equal(1, root["key"]);
    }

    [Fact]
    public void MutatingMergedChildUpdatesExistingReferenceBeforePublicEvent()
    {
        using var control = new Probe { Value = 5 };
        var child = new ResourceDictionary { ["key"] = 1 };
        control.SetResourceReference(ValueProperty, "key");
        control.Resources.MergedDictionaries.Add(child);
        Assert.Equal(1, control.Value);
        int observed = 0;
        child.ResourceChanged += (_, _) => observed = control.Value;
        child["key"] = 2;
        Assert.Equal(2, observed);
        child.Remove("key");
        Assert.Equal(5, control.Value);
    }

    [Fact]
    public void HiddenChangesDoNotNotifyParentOrInvokeSetter()
    {
        using var control = new Probe();
        var child = new ResourceDictionary { ["key"] = 1 };
        control.Resources.MergedDictionaries.Add(child);
        control.Resources["key"] = 2;
        control.SetResourceReference(ValueProperty, "key");
        control.Calls = 0;
        int notifications = 0;
        control.Resources.ResourceChanged += (_, _) => notifications++;
        child["key"] = 3;
        Assert.Equal(0, notifications);
        Assert.Equal(0, control.Calls);
        control.Resources.Clear();
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void DiamondPropagatesOnceAndRetainsCorrectBranchPrecedence()
    {
        var leaf = new ResourceDictionary { ["key"] = 1 };
        var left = new ResourceDictionary { ["key"] = 7 };
        var right = new ResourceDictionary();
        left.MergedDictionaries.Add(leaf);
        right.MergedDictionaries.Add(leaf);
        using var control = new Probe();
        control.Resources.MergedDictionaries.Add(left);
        control.Resources.MergedDictionaries.Add(right);
        control.SetResourceReference(ValueProperty, "key");
        control.Calls = 0;
        int notifications = 0;
        control.Resources.ResourceChanged += (_, _) => notifications++;
        leaf["key"] = 2;
        Assert.Equal(2, control.Value);
        Assert.Equal(1, control.Calls);
        Assert.Equal(1, notifications);
        control.Resources.MergedDictionaries.Remove(right);
        Assert.Equal(7, control.Value);
    }

    [Fact]
    public void ReplacingOrClearingMergesDetachesOldChildren()
    {
        using var control = new Probe { Value = 9 };
        var first = new ResourceDictionary { ["key"] = 1 };
        var second = new ResourceDictionary { ["key"] = 2 };
        control.Resources.MergedDictionaries.Add(first);
        control.SetResourceReference(ValueProperty, "key");
        control.Resources.MergedDictionaries[0] = second;
        first["key"] = 3;
        Assert.Equal(2, control.Value);
        control.Resources.MergedDictionaries.Clear();
        second["key"] = 4;
        Assert.Equal(9, control.Value);
    }

    [Fact]
    public void CyclesDuplicatesAndNullAreRejectedWithoutMutation()
    {
        var first = new ResourceDictionary();
        var second = new ResourceDictionary();
        first.MergedDictionaries.Add(second);
        Assert.Throws<InvalidOperationException>(() => second.MergedDictionaries.Add(first));
        Assert.Throws<InvalidOperationException>(() => first.MergedDictionaries.Add(first));
        Assert.Throws<ArgumentException>(() => first.MergedDictionaries.Add(second));
        Assert.Throws<ArgumentNullException>(() => first.MergedDictionaries.Add(null!));
        Assert.Single(first.MergedDictionaries);
        Assert.Empty(second.MergedDictionaries);
    }

    [Fact]
    public void ExtendingAlreadyMergedChildChecksAncestorDepth()
    {
        var root = new ResourceDictionary();
        ResourceDictionary tail = root;
        for (int i = 1; i < 64; i++)
        {
            var next = new ResourceDictionary();
            tail.MergedDictionaries.Add(next);
            tail = next;
        }
        Assert.Throws<InvalidOperationException>(() => tail.MergedDictionaries.Add(new ResourceDictionary()));
        Assert.Empty(tail.MergedDictionaries);
        tail["key"] = 42;
        Assert.Equal(42, root["key"]);
    }

    [Fact]
    public void DistinctGraphSizeIsBoundedAndDiamondSharingDoesNotConsumeExtraNodes()
    {
        var root = new ResourceDictionary();
        for (int i = 0; i < 255; i++)
            root.MergedDictionaries.Add(new ResourceDictionary());
        Assert.Throws<InvalidOperationException>(() => root.MergedDictionaries.Add(new ResourceDictionary()));
        Assert.Equal(255, root.MergedDictionaries.Count);
    }

    [Fact]
    public void SharedChildDoesNotKeepParentAlive()
    {
        var child = new ResourceDictionary();
        WeakReference parent = CreateParent(child);
        for (int i = 0; i < 3 && parent.IsAlive; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        }
        Assert.False(parent.IsAlive);
        child["key"] = 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateParent(ResourceDictionary child)
    {
        var parent = new ResourceDictionary();
        parent.MergedDictionaries.Add(child);
        return new WeakReference(parent);
    }

    [Fact]
    public void TypedDescriptorSupportsInheritedPropertyAndOriginalFallback()
    {
        using var control = new DerivedProbe { Value = 3 };
        control.Resources["first"] = 8;
        control.Resources["second"] = 13;
        control.SetResourceReference(ValueProperty, "first");
        control.SetResourceReference(ValueProperty, "second");
        Assert.Equal(13, control.Value);
        Assert.True(control.ClearResourceReference(ValueProperty.Name));
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void TypedDescriptorValidatesTargetAndValueWithoutInstallingInvalidReference()
    {
        using var wrong = new Control();
        Assert.Throws<ArgumentException>(() => wrong.SetResourceReference(ValueProperty, "key"));
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = "a representative text value";
        Assert.Throws<InvalidOperationException>(() => control.SetResourceReference(ValueProperty, "key"));
        Assert.False(control.ClearResourceReference(ValueProperty.Name));
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void RuntimeWrongTypeAndSetterFailureRestoreFallbackAndReportDiagnostics()
    {
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = 8;
        control.SetResourceReference(ValueProperty, "key");
        var errors = new List<ResourceReferenceErrorEventArgs>();
        control.ResourceReferenceFailed += (_, args) => errors.Add(args);
        control.Resources["key"] = "text";
        Assert.Equal(3, control.Value);
        control.Resources["key"] = 13;
        Assert.Equal(13, control.Value);
        control.Reject = true;
        control.Resources["key"] = 21;
        Assert.Equal(13, control.Value); // both setter and fallback were rejected
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void FactoryIsEvaluatedOnceAndResultIsSharedUntilExplicitReplacement()
    {
        var resources = new ResourceDictionary();
        int calls = 0;
        var value = new DisposableProbe();
        Assert.Same(value, resources.SetFactory("key", () => { calls++; return value; }));
        Assert.Same(value, resources["key"]);
        Assert.Same(value, resources["key"]);
        resources.Clear();
        Assert.Equal(1, calls);
        Assert.False(value.Disposed);
        value.Dispose();
    }

    [Fact]
    public void FactoryNullIsStoredAndFailurePreservesPreviousEntry()
    {
        var resources = new ResourceDictionary { ["key"] = 1 };
        Assert.Throws<FormatException>(() => resources.SetFactory("key", () => throw new FormatException("Representative creation failure.")));
        Assert.Equal(1, resources["key"]);
        resources.SetFactory("key", static () => null);
        Assert.True(resources.ContainsKey("key"));
        Assert.Null(resources["key"]);
    }

    [Fact]
    public void FactoryCannotOverwriteReentrantMutation()
    {
        var resources = new ResourceDictionary { ["key"] = 1 };
        Assert.Throws<InvalidOperationException>(() => resources.SetFactory("key", () =>
        {
            resources["key"] = 2;
            return 3;
        }));
        Assert.Equal(2, resources["key"]);
    }

    [Fact]
    public void NestedFactoryCallsAreBoundedAcrossDictionariesAndGuardRecoversAfterFailure()
    {
        var resources = new ResourceDictionary();
        int depth = 0;
        object? Recurse()
        {
            depth++;
            return new ResourceDictionary().SetFactory("key", Recurse);
        }
        Assert.Throws<InvalidOperationException>(() => resources.SetFactory("key", Recurse));
        Assert.Equal(32, depth);
        resources.SetFactory("key", static () => 42);
        Assert.Equal(42, resources["key"]);
    }

    [Fact]
    public void NewerReferenceInstalledByDiagnosticHandlerOwnsProperty()
    {
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = 8;
        control.Resources["new"] = 13;
        control.SetResourceReference(ValueProperty, "key");
        control.ResourceReferenceFailed += (_, _) => control.SetResourceReference(ValueProperty, "new");
        control.Resources["key"] = "text";
        Assert.Equal(13, control.Value);
        control.Resources["new"] = 21;
        Assert.Equal(21, control.Value);
    }

    [Fact]
    public void NestedResourceUpdateFromGetterDoesNotWriteOlderValue()
    {
        using var control = new Probe();
        control.Resources["key"] = 1;
        control.SetResourceReference(ValueProperty, "key");
        control.OnGet = () => control.Resources["key"] = 3;
        control.Resources["key"] = 2;
        Assert.Equal(3, control.Value);
        control.Resources["key"] = 2;
        Assert.Equal(2, control.Value);
    }

    [Fact]
    public void SetterReplacingReferenceAndThrowingDoesNotRemoveNewerReference()
    {
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = 8;
        control.Resources["new"] = 13;
        control.OnSet = () =>
        {
            control.SetResourceReference(ValueProperty, "new");
            throw new InvalidOperationException("Representative outer setter failure.");
        };
        control.SetResourceReference(ValueProperty, "key");
        Assert.Equal(13, control.Value);
        control.Resources["new"] = 21;
        Assert.Equal(21, control.Value);
    }

    [Fact]
    public void InitialGetterMayInstallNewerReferenceWithoutOuterOverwrite()
    {
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = 8;
        control.Resources["new"] = 13;
        control.OnGet = () => control.SetResourceReference(ValueProperty, "new");
        control.SetResourceReference(ValueProperty, "key");
        Assert.Equal(13, control.Value);
        control.ClearResourceReference(ValueProperty.Name);
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void ReparentingDropsMergedAncestorScopeAndDisposalDetachesReference()
    {
        using var first = new Control();
        using var second = new Control();
        using var control = new Probe { Value = 5 };
        var child = new ResourceDictionary { ["key"] = 8 };
        first.Resources.MergedDictionaries.Add(child);
        first.Controls.Add(control);
        control.SetResourceReference(ValueProperty, "key");
        second.Controls.Add(control);
        Assert.Equal(5, control.Value);
        child["key"] = 13;
        Assert.Equal(5, control.Value);
        control.Dispose();
        child["key"] = 21;
        Assert.Equal(5, control.Value);
    }

    [Fact]
    public void FactoryReplacementFlowsThroughMergedReferenceAndKeepsCallerOwnership()
    {
        var child = new ResourceDictionary();
        using var control = new Probe { Value = 3 };
        control.Resources.MergedDictionaries.Add(child);
        control.SetResourceReference(ValueProperty, "key");
        child.SetFactory("key", static () => 8);
        Assert.Equal(8, control.Value);
        child.SetFactory("key", static () => 13);
        Assert.Equal(13, control.Value);
        Assert.Throws<FormatException>(() => child.SetFactory("key", () => throw new FormatException("Representative failure.")));
        Assert.Equal(13, control.Value);
    }

    [Fact]
    public void NoOpMergeRemovalDuringFactoryDoesNotInvalidateRegistration()
    {
        var resources = new ResourceDictionary();
        resources.SetFactory("key", () =>
        {
            Assert.False(resources.MergedDictionaries.Remove(new ResourceDictionary()));
            return 8;
        });
        Assert.Equal(8, resources["key"]);
    }

    [Fact]
    public void MergeObserverFailureDoesNotLeaveOtherKeysUnrefreshed()
    {
        using var first = new Probe { Value = 3 };
        using var second = new Probe { Value = 5 };
        var shared = new ResourceDictionary { ["first"] = 8, ["second"] = 13 };
        first.Resources.MergedDictionaries.Add(shared);
        second.Resources.MergedDictionaries.Add(first.Resources);
        first.SetResourceReference(ValueProperty, "first");
        second.SetResourceReference(ValueProperty, "second");
        first.Resources.ResourceChanged += (_, _) => throw new FormatException("Representative observer failure.");
        Assert.Throws<AggregateException>(() => first.Resources.MergedDictionaries.Clear());
        Assert.Equal(3, first.Value);
        Assert.Equal(5, second.Value);
    }

    [Fact]
    public void FailureHandlerCannotPreventOtherReferencesFromReceivingCommittedChange()
    {
        using var first = new Probe { Value = 3 };
        using var second = new Probe { Value = 5 };
        string key = "ResourceCompositionTests." + Guid.NewGuid().ToString("N");
        Application.Resources[key] = 8;
        try
        {
            second.SetResourceReference(ValueProperty, key);
            first.SetResourceReference(ValueProperty, key);
            first.ResourceReferenceFailed += (_, _) => throw new FormatException("Representative handler failure.");
            second.ResourceReferenceFailed += (_, _) => second.Text = "received";
            Assert.Throws<FormatException>(() => Application.Resources[key] = "text");
            Assert.Equal("received", second.Text);
        }
        finally { Application.Resources.Remove(key); }
    }

    [Fact]
    public void DisposingFromInitialGetterCannotInstallAReference()
    {
        using var control = new Probe();
        control.OnGet = control.Dispose;
        control.SetResourceReference(ValueProperty, "key");
        Assert.False(control.ClearResourceReference(ValueProperty.Name));
        Assert.Throws<ObjectDisposedException>(() => control.SetResourceReference(ValueProperty, "key"));
    }

    [Theory]
    [InlineData("MissingRepresentativeProperty")]
    [InlineData(nameof(Probe.WriteOnlyValue))]
    [InlineData(nameof(Probe.ReadOnlyValue))]
    [InlineData(nameof(Probe.StaticValue))]
    [InlineData("Item")]
    public void LegacyNamesFailClearlyForUnavailableOrInvalidProperties(string property)
    {
        using var control = new Probe();
        ArgumentException failure = Assert.Throws<ArgumentException>(() => control.SetResourceReference(property, "key"));
        Assert.Contains(property, failure.Message, StringComparison.Ordinal);
        Assert.False(control.ClearResourceReference(property));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TypedDescriptorsRejectEmptyRepresentativeNames(string name)
        => Assert.Throws<ArgumentException>(() => new ResourceProperty<Probe, int>(name,
            static control => control.Value, static (control, value) => control.Value = value));

    [Fact]
    public void FactoryObserverFailureOccursAfterCommitWithoutRepeatingCreation()
    {
        var resources = new ResourceDictionary();
        int calls = 0;
        resources.ResourceChanged += (_, _) => throw new FormatException("Representative observer failure.");
        Assert.Throws<FormatException>(() => resources.SetFactory("key", () => { calls++; return 8; }));
        Assert.Equal(1, calls);
        Assert.Equal(8, resources["key"]);
    }

    [Fact]
    public void OneBindingVisibleThroughTwoScopesReportsOneDiagnostic()
    {
        using var parent = new Control();
        using var control = new Probe { Value = 3 };
        parent.Controls.Add(control);
        parent.Resources.MergedDictionaries.Add(control.Resources);
        control.Resources["key"] = 8;
        control.SetResourceReference(ValueProperty, "key");
        int errors = 0;
        control.ResourceReferenceFailed += (_, _) => errors++;
        control.Resources["key"] = "representative text";
        Assert.Equal(1, errors);
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void ClearingReferenceDetachesBeforeFallbackSetterMutatesResources()
    {
        using var control = new Probe { Value = 3 };
        control.Resources["key"] = 8;
        control.SetResourceReference(ValueProperty, "key");
        control.OnSet = () => control.Resources["key"] = 13;
        Assert.True(control.ClearResourceReference(ValueProperty.Name));
        Assert.Equal(3, control.Value);
        control.Resources["key"] = 21;
        Assert.Equal(3, control.Value);
    }

    [Fact]
    public void LegacyPublicPropertyWithPrivateGetterRemainsSupportedInJit()
    {
        using var control = new Probe();
        control.Resources["key"] = 8;
        control.SetResourceReference(nameof(Probe.Unreadable), "key");
        Assert.Equal(8, control.ReadPrivateGetter());
        control.ClearResourceReference(nameof(Probe.Unreadable));
        Assert.Equal(0, control.ReadPrivateGetter());
    }

    private class Probe : Control
    {
        private int value;
        public int Calls { get; set; }
        public bool Reject { get; set; }
        public Action? OnGet { get; set; }
        public Action? OnSet { get; set; }
        public int Unreadable { private get; set; }
        public int WriteOnlyValue { set { } }
        public int ReadPrivateGetter() => Unreadable;
        public int ReadOnlyValue => 1;
        public static int StaticValue { get; set; }
        public int this[int index] { get => index; set { } }
        public int Value
        {
            get
            {
                Action? action = OnGet;
                OnGet = null;
                action?.Invoke();
                return value;
            }
            set
            {
                if (Reject) throw new InvalidOperationException("Representative setter failure.");
                this.value = value;
                Calls++;
                Action? action = OnSet;
                OnSet = null;
                action?.Invoke();
            }
        }
    }
    private sealed class DerivedProbe : Probe;
    private sealed class DisposableProbe : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
