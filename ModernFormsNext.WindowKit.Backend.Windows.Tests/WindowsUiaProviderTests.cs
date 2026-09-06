using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ModernFormsNext.WindowKit.Backend.Windows.Win32;
using ModernFormsNext.WindowKit.Platform.Accessibility;
using Xunit;
using PlatformRect = ModernFormsNext.WindowKit.Rect;

namespace ModernFormsNext.WindowKit.Backend.Windows.Tests;

public sealed class WindowsUiaProviderTests
{
    private const int ActionInvoke = 1 << 0;
    private const int ActionToggle = 1 << 1;
    private const int ActionSelect = 1 << 2;
    private const int ActionExpand = 1 << 3;
    private const int ActionCollapse = 1 << 4;
    private const int ActionSetValue = 1 << 5;
    private const int ActionScrollIntoView = 1 << 9;
    private const int ActionFocus = 1 << 10;

    private const int StateUnavailable = 0x1;
    private const int StateSelected = 0x2;
    private const int StateFocused = 0x4;
    private const int StateChecked = 0x10;
    private const int StateMixed = 0x20;
    private const int StateReadOnly = 0x40;
    private const int StateExpanded = 0x200;
    private const int StateCollapsed = 0x400;
    private const int StateOffscreen = 0x10000;
    private const int StateFocusable = 0x100000;
    private const int StateMultiSelectable = 0x1000000;

    [Fact]
    public void RootProviderMapsCanonicalProperties()
    {
        TestAccessibleObject root = Root(
            name: "Settings",
            controlType: 23,
            actions: ActionFocus,
            state: StateFocusable | StateFocused);
        root.AutomationId = "SettingsDialog";
        root.Help = "Application settings";
        root.ClassName = "SettingsForm";
        root.Bounds = new PlatformRect(100, 120, 640, 480);

        using WindowsUiaRootProvider provider = Create(root);

        Assert.Equal("Settings", Property(provider, WindowsUiaIds.NameProperty));
        Assert.Equal("SettingsDialog", Property(provider, WindowsUiaIds.AutomationIdProperty));
        Assert.Equal(50032, Property(provider, WindowsUiaIds.ControlTypeProperty));
        Assert.Equal(true, Property(provider, WindowsUiaIds.IsEnabledProperty));
        Assert.Equal(true, Property(provider, WindowsUiaIds.IsKeyboardFocusableProperty));
        Assert.Equal(true, Property(provider, WindowsUiaIds.HasKeyboardFocusProperty));
        Assert.Equal("Application settings", Property(provider, WindowsUiaIds.HelpTextProperty));
        Assert.Equal("SettingsForm", Property(provider, WindowsUiaIds.ClassNameProperty));
        Assert.Equal("ModernFormsNext", Property(provider, WindowsUiaIds.FrameworkIdProperty));
        Assert.Null(Property(provider, WindowsUiaIds.ValueProperty));
    }

    [Theory]
    [InlineData(1, 50025)]
    [InlineData(2, 50032)]
    [InlineData(3, 50033)]
    [InlineData(4, 50026)]
    [InlineData(5, 50020)]
    [InlineData(6, 50000)]
    [InlineData(7, 50002)]
    [InlineData(8, 50013)]
    [InlineData(9, 50002)]
    [InlineData(10, 50004)]
    [InlineData(11, 50003)]
    [InlineData(12, 50008)]
    [InlineData(13, 50007)]
    [InlineData(14, 50023)]
    [InlineData(15, 50024)]
    [InlineData(16, 50018)]
    [InlineData(17, 50019)]
    [InlineData(18, 50009)]
    [InlineData(19, 50011)]
    [InlineData(20, 50015)]
    [InlineData(21, 50012)]
    [InlineData(22, 50014)]
    [InlineData(23, 50032)]
    [InlineData(24, 50006)]
    [InlineData(25, 50021)]
    [InlineData(26, 50038)]
    public void ControlTypesMapFromCanonicalIds(int canonicalType, int expectedUiaType)
        => Assert.Equal(expectedUiaType, WindowsUiaControlTypeMapper.Map(canonicalType));

    [Fact]
    public void FragmentNavigationUsesCanonicalParentsChildrenAndSiblings()
    {
        TestAccessibleObject root = Root("Root", controlType: 2);
        TestAccessibleObject first = root.AddChild(new TestAccessibleObject("Logical item", 13));
        TestAccessibleObject custom = root.AddChild(new TestAccessibleObject("Custom child", 1));
        using WindowsUiaRootProvider provider = Create(root);

        var firstProvider = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        var customProvider = Assert.IsType<WindowsUiaProvider>(firstProvider.Navigate(NavigateDirection.NextSibling));

        Assert.Same(root, Assert.IsType<WindowsUiaRootProvider>(firstProvider.Navigate(NavigateDirection.Parent)).PlatformObject);
        Assert.Same(custom, customProvider.PlatformObject);
        Assert.Same(first, Assert.IsType<WindowsUiaProvider>(customProvider.Navigate(NavigateDirection.PreviousSibling)).PlatformObject);
        Assert.Same(customProvider, provider.Navigate(NavigateDirection.LastChild));
    }

    [Fact]
    public void ReorderPreservesProviderIdentityAndRemovalMakesProviderUnavailable()
    {
        TestAccessibleObject root = Root("Root", controlType: 2);
        TestAccessibleObject first = root.AddChild(new TestAccessibleObject("First", 13));
        TestAccessibleObject second = root.AddChild(new TestAccessibleObject("Second", 13));
        using WindowsUiaRootProvider provider = Create(root);

        var firstProvider = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        root.Children.Reverse();

        Assert.Same(firstProvider, provider.Navigate(NavigateDirection.LastChild));
        root.Children.Remove(first);
        first.ParentObject = null;

        Assert.Throws<WindowsUiaElementNotAvailableException>(() => firstProvider.GetPropertyValue(WindowsUiaIds.NameProperty));
        Assert.Same(second, Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild)).PlatformObject);
    }

    [Fact]
    public void BoundingRectangleUsesPhysicalCanonicalScreenPixelsWithoutDoubleScaling()
    {
        TestAccessibleObject root = Root("Root", controlType: 2);
        root.Bounds = new PlatformRect(150, 225, 450, 300);
        using WindowsUiaRootProvider provider = Create(root);

        UiaRect bounds = provider.BoundingRectangle;

        Assert.Equal(new UiaRect(150, 225, 450, 300), bounds);
        Assert.Equal(false, Property(provider, WindowsUiaIds.IsOffscreenProperty));
        root.State |= StateOffscreen;
        Assert.Equal(true, Property(provider, WindowsUiaIds.IsOffscreenProperty));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, true)]
    [InlineData(4, false, false)]
    public void AccessibilityViewMapsToControlAndContentProperties(
        int view,
        bool expectedControl,
        bool expectedContent)
    {
        TestAccessibleObject root = Root("Root", controlType: 2);
        root.View = view;
        using WindowsUiaRootProvider provider = Create(root);

        Assert.Equal(expectedControl, Property(provider, WindowsUiaIds.IsControlElementProperty));
        Assert.Equal(expectedContent, Property(provider, WindowsUiaIds.IsContentElementProperty));
    }

    [Fact]
    public void PatternsAreAdvertisedOnlyForCanonicalCapabilities()
    {
        TestAccessibleObject node = Root("Node", controlType: 10, actions: ActionInvoke | ActionSetValue);
        using WindowsUiaRootProvider provider = Create(node);

        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaIds.InvokePattern));
        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaIds.ValuePattern));
        Assert.Null(provider.GetPatternProvider(WindowsUiaIds.TogglePattern));
        Assert.Null(provider.GetPatternProvider(10004));
    }

    [Fact]
    public void InvokeRoutesThroughCanonicalAction()
    {
        TestAccessibleObject node = Root("Action", controlType: 6, actions: ActionInvoke);
        using WindowsUiaRootProvider provider = Create(node);

        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaIds.InvokePattern));
        ((IInvokeProvider)provider).Invoke();

        Assert.Equal(ActionInvoke, node.LastAction);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(StateChecked, 1)]
    [InlineData(StateMixed, 2)]
    public void ToggleMapsStateAndRoutesCanonicalAction(int state, int expected)
    {
        TestAccessibleObject node = Root("Toggle", controlType: 7, actions: ActionToggle, state: state);
        using WindowsUiaRootProvider provider = Create(node);
        var toggle = (IToggleProvider)provider;

        Assert.Equal((ToggleState)expected, toggle.ToggleState);
        toggle.Toggle();
        Assert.Equal(ActionToggle, node.LastAction);
    }

    [Fact]
    public void ValuePatternRespectsReadOnlyAndPasswordPrivacy()
    {
        TestAccessibleObject editable = Root("Editor", controlType: 10, actions: ActionSetValue);
        editable.Value = "before";
        using WindowsUiaRootProvider editableProvider = Create(editable);
        var valueProvider = (IValueProvider)editableProvider;

        Assert.False(valueProvider.IsReadOnly);
        Assert.Equal("before", valueProvider.Value);
        valueProvider.SetValue("after");
        Assert.Equal("after", editable.Value);

        TestAccessibleObject password = Root("Password", controlType: 10, actions: ActionSetValue);
        password.IsSensitive = true;
        password.Value = "secret";
        using WindowsUiaRootProvider passwordProvider = Create(password);
        var passwordValue = (IValueProvider)passwordProvider;

        Assert.Equal(true, Property(passwordProvider, WindowsUiaIds.IsPasswordProperty));
        Assert.Throws<WindowsUiaAccessDeniedException>(() => passwordProvider.GetPropertyValue(WindowsUiaIds.ValueProperty));
        Assert.Throws<WindowsUiaAccessDeniedException>(() => passwordValue.Value);
        passwordValue.SetValue("replacement");
        Assert.Equal("replacement", password.Value);

        TestAccessibleObject readOnly = Root("Read only", controlType: 10, state: StateReadOnly);
        readOnly.Value = "visible";
        using WindowsUiaRootProvider readOnlyProvider = Create(readOnly);
        var readOnlyValue = Assert.IsAssignableFrom<IValueProvider>(
            readOnlyProvider.GetPatternProvider(WindowsUiaIds.ValuePattern));
        Assert.True(readOnlyValue.IsReadOnly);
        Assert.Equal("visible", readOnlyValue.Value);
        Assert.Throws<InvalidOperationException>(() => readOnlyValue.SetValue("rejected"));
    }

    [Fact]
    public void RangeValueMapsMetadataAndRoutesSetValue()
    {
        TestAccessibleObject node = Root("Volume", controlType: 20, actions: ActionSetValue);
        node.RangeValue = new PlatformAccessibleRangeValue(25, 0, 100, 1, 10, false);
        using WindowsUiaRootProvider provider = Create(node);
        var range = (IRangeValueProvider)provider;

        Assert.Equal(25, range.Value);
        Assert.Equal(0, range.Minimum);
        Assert.Equal(100, range.Maximum);
        Assert.Equal(1, range.SmallChange);
        Assert.Equal(10, range.LargeChange);
        Assert.False(range.IsReadOnly);
        range.SetValue(75);
        Assert.Equal(75, node.RangeValue?.Value);
    }

    [Fact]
    public void ReadOnlyRangeRejectsMutation()
    {
        TestAccessibleObject node = Root("Progress", controlType: 21);
        node.State = StateReadOnly;
        node.RangeValue = new PlatformAccessibleRangeValue(50, 0, 100, 1, 10, true);
        using WindowsUiaRootProvider provider = Create(node);

        Assert.Same(provider, provider.GetPatternProvider(WindowsUiaIds.RangeValuePattern));
        Assert.Throws<InvalidOperationException>(() => ((IRangeValueProvider)provider).SetValue(60));
    }

    [Theory]
    [InlineData(StateExpanded, 1)]
    [InlineData(StateCollapsed, 0)]
    public void ExpandCollapseMapsStateAndCanonicalActions(int state, int expected)
    {
        TestAccessibleObject node = Root("Branch", controlType: 15, actions: ActionExpand | ActionCollapse, state: state);
        using WindowsUiaRootProvider provider = Create(node);
        var pattern = (IExpandCollapseProvider)provider;

        Assert.Equal((ExpandCollapseState)expected, pattern.ExpandCollapseState);
        pattern.Expand();
        Assert.Equal(ActionExpand, node.LastAction);
        pattern.Collapse();
        Assert.Equal(ActionCollapse, node.LastAction);
    }

    [Fact]
    public void SelectionContainerReturnsCanonicalSelectedChildren()
    {
        TestAccessibleObject list = Root("List", controlType: 12, state: StateMultiSelectable);
        list.AddChild(new TestAccessibleObject("One", 13));
        TestAccessibleObject selected = list.AddChild(new TestAccessibleObject("Two", 13) { State = StateSelected });
        using WindowsUiaRootProvider provider = Create(list);
        var selection = (ISelectionProvider)provider;

        IRawElementProviderSimple item = Assert.Single(selection.GetSelection());

        Assert.True(selection.CanSelectMultiple);
        Assert.False(selection.IsSelectionRequired);
        Assert.Same(selected, Assert.IsType<WindowsUiaProvider>(item).PlatformObject);
    }

    [Fact]
    public void SelectionItemUsesCanonicalSelectionPath()
    {
        TestAccessibleObject list = Root("List", controlType: 12);
        TestAccessibleObject item = list.AddChild(new TestAccessibleObject("Item", 13)
        {
            SupportedActions = ActionSelect
        });
        using WindowsUiaRootProvider provider = Create(list);
        var itemProvider = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        var selectionItem = (ISelectionItemProvider)itemProvider;

        Assert.Same(provider, selectionItem.SelectionContainer);
        selectionItem.Select();
        Assert.Equal(ActionSelect, item.LastAction);
    }

    [Fact]
    public void ScrollItemUsesCanonicalScrollIntoViewAction()
    {
        TestAccessibleObject list = Root("List", controlType: 12);
        TestAccessibleObject item = list.AddChild(new TestAccessibleObject("Item", 13)
        {
            SupportedActions = ActionScrollIntoView
        });
        using WindowsUiaRootProvider provider = Create(list);
        var itemProvider = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));

        Assert.Same(itemProvider, itemProvider.GetPatternProvider(WindowsUiaIds.ScrollItemPattern));
        ((IScrollItemProvider)itemProvider).ScrollIntoView();
        Assert.Equal(ActionScrollIntoView, item.LastAction);
        Assert.Null(provider.GetPatternProvider(10004));
    }

    [Fact]
    public void FocusAndHitTestingUseCanonicalSemanticTree()
    {
        TestAccessibleObject root = Root("Root", controlType: 2);
        root.Bounds = new PlatformRect(0, 0, 500, 500);
        TestAccessibleObject child = root.AddChild(new TestAccessibleObject("Focus target", 6)
        {
            Bounds = new PlatformRect(20, 30, 100, 40),
            State = StateFocusable | StateFocused,
            SupportedActions = ActionFocus
        });
        root.Focused = child;
        using WindowsUiaRootProvider provider = Create(root);

        Assert.Same(child, Assert.IsType<WindowsUiaProvider>(provider.GetFocus()).PlatformObject);
        Assert.Same(child, Assert.IsType<WindowsUiaProvider>(provider.ElementProviderFromPoint(25, 35)).PlatformObject);

        var childProvider = Assert.IsType<WindowsUiaProvider>(provider.Navigate(NavigateDirection.FirstChild));
        childProvider.SetFocus();
        Assert.Equal(ActionFocus, child.LastAction);
    }

    [Fact]
    public async Task BackgroundCallbacksUseDispatcherBoundary()
    {
        TestAccessibleObject root = Root("Root", controlType: 2, actions: ActionInvoke);
        var dispatcher = new RecordingDispatcher();
        using WindowsUiaRootProvider provider = WindowsUiaRootProvider.Create(new IntPtr(42), root, dispatcher);

        object? name = await Task.Run(() => provider.GetPropertyValue(WindowsUiaIds.NameProperty));
        await Task.Run(() => ((IInvokeProvider)provider).Invoke());

        Assert.Equal("Root", name);
        Assert.Equal(2, dispatcher.InvokeCount);
    }

    [Fact]
    public void DisposedWindowMakesProviderUnavailable()
    {
        WindowsUiaRootProvider provider = Create(Root("Root", controlType: 2));
        provider.Dispose();

        Assert.Throws<WindowsUiaElementNotAvailableException>(() => provider.GetPropertyValue(WindowsUiaIds.NameProperty));

        int result = provider.AbiGetPropertyValue(WindowsUiaIds.NameProperty, out WindowsUiaVariant value);
        Assert.Equal(unchecked((int)0x80040201), result);
        value.Dispose();
    }

    [Fact]
    public void CanonicalNotificationsMapToNativeUiaEventsWithoutSensitiveValues()
    {
        var eventSink = new RecordingEventSink();
        TestAccessibleObject node = Root(
            "Toggle",
            controlType: 7,
            actions: ActionToggle,
            state: StateChecked);
        using WindowsUiaRootProvider provider = WindowsUiaRootProvider.Create(
            new IntPtr(42),
            node,
            new InlineDispatcher(),
            eventSink);

        provider.RaiseNotification(node, 0x8005);
        provider.RaiseNotification(node, 0x800C);
        provider.RaiseNotification(node, 0x800A);
        provider.RaiseNotification(node, 0x800B);
        provider.RaiseNotification(node, 0x8006);
        provider.RaiseNotification(node, 0x8004);
        provider.RaiseNotification(node, 0x8002);
        provider.RaiseNotification(node, 0x8003);

        Assert.Contains(WindowsUiaIds.AutomationFocusChangedEvent, eventSink.AutomationEvents);
        Assert.Contains(WindowsUiaIds.ElementSelectedEvent, eventSink.AutomationEvents);
        Assert.Contains(WindowsUiaIds.NameProperty, eventSink.PropertyChanges);
        Assert.Contains(WindowsUiaIds.IsEnabledProperty, eventSink.PropertyChanges);
        Assert.Contains(WindowsUiaIds.ToggleStateProperty, eventSink.PropertyChanges);
        Assert.Contains(WindowsUiaIds.BoundingRectangleProperty, eventSink.PropertyChanges);
        Assert.Contains(StructureChangeType.ChildAdded, eventSink.StructureChanges);
        Assert.Contains(StructureChangeType.ChildRemoved, eventSink.StructureChanges);

        node.Value = "safe-value";
        provider.RaiseNotification(node, 0x800E);
        Assert.Contains(WindowsUiaIds.ValueProperty, eventSink.PropertyChanges);
        Assert.Contains("safe-value", eventSink.Values);

        int propertyChangeCount = eventSink.PropertyChanges.Count;
        node.IsSensitive = true;
        node.Value = "must-not-be-emitted";
        provider.RaiseNotification(node, 0x800E);
        Assert.Equal(propertyChangeCount, eventSink.PropertyChanges.Count);
        Assert.DoesNotContain(eventSink.Values, value => Equals(value, "must-not-be-emitted"));
    }

    [Fact]
    public void ReorderNotificationsDifferentiateAddRemoveAndReorderWithoutCachingNodes()
    {
        var eventSink = new RecordingEventSink();
        TestAccessibleObject root = Root("List", controlType: 12);
        TestAccessibleObject first = root.AddChild(new TestAccessibleObject("First", 13));
        TestAccessibleObject second = root.AddChild(new TestAccessibleObject("Second", 13));
        using WindowsUiaRootProvider provider = WindowsUiaRootProvider.Create(
            new IntPtr(42),
            root,
            new InlineDispatcher(),
            eventSink);

        root.AddChild(new TestAccessibleObject("Added", 13));
        provider.RaiseNotification(root, 0x8004);
        WindowsUiaStructureEvent addEvent = eventSink.StructureEvents[^1];
        Assert.Equal(StructureChangeType.ChildAdded, addEvent.ChangeType);
        Assert.Same(provider, addEvent.Provider);
        Assert.Null(addEvent.RuntimeId);

        root.Children.Remove(first);
        first.ParentObject = null;
        provider.RaiseNotification(root, 0x8004);
        WindowsUiaStructureEvent removeEvent = eventSink.StructureEvents[^1];
        Assert.Equal(StructureChangeType.ChildRemoved, removeEvent.ChangeType);
        Assert.Same(provider, removeEvent.Provider);
        Assert.Equal(WindowsUiaProvider.CreateRuntimeId(first.RuntimeId), removeEvent.RuntimeId);

        root.Children.Reverse();
        provider.RaiseNotification(root, 0x8004);
        WindowsUiaStructureEvent reorderEvent = eventSink.StructureEvents[^1];
        Assert.Equal(StructureChangeType.ChildrenReordered, reorderEvent.ChangeType);
        Assert.Same(provider, reorderEvent.Provider);
        Assert.Null(reorderEvent.RuntimeId);
        Assert.Contains(second, root.Children);
    }

    [Fact]
    public void ComCallableWrapperExposesSimpleFragmentRootAndPatternInterfaces()
    {
        using WindowsUiaRootProvider provider = Create(Root("Root", controlType: 6, actions: ActionInvoke));
        IntPtr unknown = WindowsUiaNativeMethods.GetUnknownPointer(provider);

        try
        {
            AssertComInterface(unknown, new Guid("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C"));
            AssertComInterface(unknown, new Guid("F7063DA8-8359-439C-9297-BBC5299A7D87"));
            AssertComInterface(unknown, new Guid("620CE2A5-AB8F-40A9-86CB-DE3C75599B58"));
            AssertComInterface(unknown, new Guid("54FCB24B-E18E-47A2-B4D3-ECCBE77599A2"));
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    [Fact]
    public async Task RealHwndIsConsumableThroughTheWindowsUiAutomationClient()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string repositoryRoot = FindRepositoryRoot();
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string hostPath = System.IO.Path.Combine(
            repositoryRoot,
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost",
            "bin",
            configuration,
            "net10.0-windows",
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll");
        string clientPath = System.IO.Path.Combine(
            repositoryRoot,
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient",
            "bin",
            configuration,
            "net10.0-windows",
            "ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient.dll");

        Assert.True(File.Exists(hostPath), $"The UIA integration host was not built: {hostPath}");
        Assert.True(File.Exists(clientPath), $"The UIA integration client was not built: {clientPath}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using Process host = StartDotnetProcess(hostPath);
        IntPtr hwnd = IntPtr.Zero;
        int? hostExitCode = null;

        try
        {
            string? handleLine = await ReadLineStartingWithAsync(host, "HWND:", timeout.Token);
            Assert.NotNull(handleLine);
            Assert.True(long.TryParse(handleLine.AsSpan("HWND:".Length), out long rawHandle));
            hwnd = new IntPtr(rawHandle);
            Assert.NotEqual(IntPtr.Zero, hwnd);

            using Process client = StartDotnetProcess(clientPath, rawHandle.ToString());
            Task<string> clientOutputTask = client.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> clientErrorTask = client.StandardError.ReadToEndAsync(timeout.Token);
            await client.WaitForExitAsync(timeout.Token);
            string clientOutput = await clientOutputTask;
            string clientError = await clientErrorTask;

            Assert.True(
                client.ExitCode == 0,
                $"The real UIA client exited with {client.ExitCode}: {clientError}");

            using JsonDocument result = JsonDocument.Parse(clientOutput);
            JsonElement root = result.RootElement;
            Assert.Equal("ModernFormsNext UIA integration window", root.GetProperty("RootNameBeforeInvoke").GetString());
            Assert.Equal("ModernFormsNext UIA action invoked", root.GetProperty("RootNameAfterInvoke").GetString());
            Assert.Equal("Invoke integration action", root.GetProperty("ButtonName").GetString());
            Assert.Equal("uia.integration.invoke", root.GetProperty("AutomationId").GetString());
            Assert.Equal(50032, root.GetProperty("RootControlType").GetInt32());
            Assert.Equal(50000, root.GetProperty("ButtonControlType").GetInt32());
            Assert.True(root.GetProperty("HasKeyboardFocus").GetBoolean());
            Assert.False(root.GetProperty("DisabledCommandIsEnabled").GetBoolean());

            Assert.Equal("INVOKED", await ReadLineStartingWithAsync(host, "INVOKED", timeout.Token));
        }
        finally
        {
            if (hwnd != IntPtr.Zero)
                _ = PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);

            try
            {
                await host.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                await host.WaitForExitAsync();
            }

            if (host.HasExited)
                hostExitCode = host.ExitCode;
        }

        Assert.Equal(0, hostExitCode);
    }

    private static WindowsUiaRootProvider Create(TestAccessibleObject root)
        => WindowsUiaRootProvider.Create(new IntPtr(42), root, new InlineDispatcher());

    private static TestAccessibleObject Root(
        string name,
        int controlType,
        int actions = 0,
        int state = 0)
        => new(name, controlType)
        {
            SupportedActions = actions,
            State = state,
            View = 2,
            Bounds = new PlatformRect(0, 0, 800, 600)
        };

    private static object? Property(WindowsUiaProvider provider, int propertyId)
        => provider.GetPropertyValue(propertyId);

    private static void AssertComInterface(IntPtr unknown, Guid interfaceId)
    {
        int result = Marshal.QueryInterface(unknown, in interfaceId, out IntPtr instance);
        try
        {
            Assert.Equal(0, result);
            Assert.NotEqual(IntPtr.Zero, instance);
        }
        finally
        {
            if (instance != IntPtr.Zero)
                _ = Marshal.Release(instance);
        }
    }

    private static Process StartDotnetProcess(string assemblyPath, string? argument = null)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(assemblyPath);
        if (argument is not null)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start {assemblyPath}.");
    }

    private static async Task<string?> ReadLineStartingWithAsync(
        Process process,
        string prefix,
        CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line;
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "ModernFormsNext.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the ModernFormsNext repository root.");
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private sealed class InlineDispatcher : IWindowsUiaDispatcher
    {
        public bool CheckAccess() => true;

        public T Invoke<T>(Func<T> callback, TimeSpan timeout) => callback();

        public void Invoke(Action callback, TimeSpan timeout) => callback();
    }

    private sealed class RecordingDispatcher : IWindowsUiaDispatcher
    {
        private readonly int ownerThreadId = Environment.CurrentManagedThreadId;

        public int InvokeCount { get; private set; }

        public bool CheckAccess() => Environment.CurrentManagedThreadId == ownerThreadId;

        public T Invoke<T>(Func<T> callback, TimeSpan timeout)
        {
            InvokeCount++;
            return callback();
        }

        public void Invoke(Action callback, TimeSpan timeout)
        {
            InvokeCount++;
            callback();
        }
    }

    private sealed class RecordingEventSink : IWindowsUiaEventSink
    {
        public bool ClientsAreListening => true;

        public List<int> AutomationEvents { get; } = [];

        public List<int> PropertyChanges { get; } = [];

        public List<object?> Values { get; } = [];

        public List<StructureChangeType> StructureChanges
            => StructureEvents.Select(static change => change.ChangeType).ToList();

        public List<WindowsUiaStructureEvent> StructureEvents { get; } = [];

        public void RaiseAutomationEvent(WindowsUiaProvider provider, int eventId)
            => AutomationEvents.Add(eventId);

        public void RaiseAutomationPropertyChangedEvent(
            WindowsUiaProvider provider,
            int propertyId,
            object? oldValue,
            object? newValue)
        {
            PropertyChanges.Add(propertyId);
            Values.Add(newValue);
        }

        public void RaiseStructureChangedEvent(
            WindowsUiaProvider provider,
            StructureChangeType changeType,
            int[]? runtimeId)
            => StructureEvents.Add(new WindowsUiaStructureEvent(provider, changeType, runtimeId));
    }

    private sealed record WindowsUiaStructureEvent(
        WindowsUiaProvider Provider,
        StructureChangeType ChangeType,
        int[]? RuntimeId);

    private sealed class TestAccessibleObject : IPlatformUiaAccessibleObject
    {
        private static long nextRuntimeId;

        public TestAccessibleObject(string name, int controlType)
        {
            Name = name;
            ControlType = controlType;
            RuntimeId = Interlocked.Increment(ref nextRuntimeId);
        }

        public List<TestAccessibleObject> Children { get; } = [];

        public TestAccessibleObject? ParentObject { get; set; }

        public IPlatformAccessibleObject? Focused { get; set; }

        public int LastAction { get; private set; }

        public int LastSelectionFlags { get; private set; }

        public long RuntimeId { get; }

        public string? AutomationId { get; set; }

        public int ControlType { get; }

        public int View { get; set; } = 2;

        public string? ClassName { get; set; }

        public PlatformRect Bounds { get; set; }

        public string? DefaultAction => null;

        public string? Description { get; set; }

        public string? Help { get; set; }

        public string? KeyboardShortcut => null;

        public string? Name { get; set; }

        public IPlatformAccessibleObject? Parent => ParentObject;

        public int Role => 0;

        public int State { get; set; }

        public bool IsSensitive { get; set; }

        public PlatformAccessibleRangeValue? RangeValue { get; set; }

        public int SupportedActions { get; set; }

        public string? Value { get; set; }

        public TestAccessibleObject AddChild(TestAccessibleObject child)
        {
            child.ParentObject = this;
            Children.Add(child);
            return child;
        }

        public void DoDefaultAction()
        {
        }

        public bool PerformAction(int action, object? parameter = null)
        {
            if ((SupportedActions & action) == 0)
                return false;

            LastAction = action;
            if (action == ActionSetValue)
            {
                if (RangeValue is { } range && parameter is double numeric)
                    RangeValue = range with { Value = numeric };
                else
                    Value = parameter as string;
            }
            else if (action == ActionSelect)
            {
                State |= StateSelected;
            }
            else if (action == ActionFocus)
            {
                State |= StateFocused;
            }

            return true;
        }

        public int GetHelpTopic(out string? fileName)
        {
            fileName = null;
            return 0;
        }

        public IPlatformAccessibleObject? GetChild(int index)
            => index >= 0 && index < Children.Count ? Children[index] : null;

        public int GetChildCount() => Children.Count;

        public IPlatformAccessibleObject? GetFocused() => Focused;

        public IPlatformAccessibleObject? GetSelected()
            => Children.FirstOrDefault(child => (child.State & StateSelected) != 0);

        public IPlatformAccessibleObject? HitTest(int x, int y)
        {
            for (int index = Children.Count - 1; index >= 0; index--)
            {
                if (Children[index].HitTest(x, y) is { } child)
                    return child;
            }

            return x >= Bounds.X
                && y >= Bounds.Y
                && x < Bounds.Right
                && y < Bounds.Bottom
                    ? this
                    : null;
        }

        public IPlatformAccessibleObject? Navigate(PlatformAccessibleNavigation direction)
        {
            if (ParentObject is null)
                return null;

            int index = ParentObject.Children.IndexOf(this);
            return direction switch
            {
                PlatformAccessibleNavigation.Next when index + 1 < ParentObject.Children.Count
                    => ParentObject.Children[index + 1],
                PlatformAccessibleNavigation.Previous when index > 0
                    => ParentObject.Children[index - 1],
                _ => null
            };
        }

        public void Select(int flags)
        {
            LastSelectionFlags = flags;
            if ((flags & 2) != 0 || (flags & 8) != 0)
                State |= StateSelected;
            if ((flags & 16) != 0)
                State &= ~StateSelected;
            if ((flags & 1) != 0)
                State |= StateFocused;
        }
    }
}
