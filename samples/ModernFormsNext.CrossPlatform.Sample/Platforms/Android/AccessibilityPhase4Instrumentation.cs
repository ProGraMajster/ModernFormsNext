using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;
using ModernFormsNext.WindowKit;
using ModernFormsNext.WindowKit.Backend.Android;
using ModernFormsNext.WindowKit.Platform;
using NativeAction = Android.Views.Accessibility.Action;
using Environment = System.Environment;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>Runs explicitly requested Phase 4 checks through the real Android UiAutomation connection.</summary>
/// <remarks>
/// The normal sample launch never runs this instrumentation. It uses existing controls and their
/// native provider, reports only constant check categories/counts, and never logs node or text payloads.
/// This is separate from the historical Phase 3 runner and does not claim physical TalkBack evidence.
/// </remarks>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.AccessibilityPhase4Instrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class AccessibilityPhase4Instrumentation : Instrumentation
{
    private const string Package = "com.programajster.modernformsnext.sample";
    private int assertions, sensitiveEvents, sensitivePayloads;

    /// <summary>Creates the explicit native runner.</summary>
    public AccessibilityPhase4Instrumentation() { }
    /// <summary>Reattaches to Android's instrumentation instance.</summary>
    /// <param name="handle">The native Java handle.</param>
    /// <param name="ownership">The JNI ownership transfer.</param>
    public AccessibilityPhase4Instrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }

    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        var automation = UiAutomation!;
        bool success = false;
        string stage = "launch";
        IPlatformAccessibilitySettings? preferences = null;
        EventHandler<PlatformAccessibilityPreferences>? preferenceHandler = null;
        int preferenceEvents = 0, wrongPreferenceThread = 0;
        automation.AccessibilityEvent += OnAccessibilityEvent;
        try
        {
            using var intent = new Intent(TargetContext!, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            intent.PutExtra("ACCESSIBILITY_PHASE4", true);
            var activity = (MainActivity)StartActivitySync(intent)!;
            Idle();
            var sharedApp = OnUi(() => ((SampleApplication)activity.Application!).SharedApp);
            var demo = OnUi(() => sharedApp.Root.Controls.OfType<AccessibilityPhase4Panel>().Single());
            using var root = automation.RootInActiveWindow;
            Check(root is not null, "native-root");
            stage = "preferences";
            var initialPreferences = OnUi(ReadPreferences);
            preferences = initialPreferences.Provider;
            Check(preferences is not null, "preferences-canonical-optional-capability");
            Check(initialPreferences.Scale > 0 && initialPreferences.ConfigurationScale > 0
                && initialPreferences.Scale == initialPreferences.ConfigurationScale, "preferences-actual-configuration-text-scale");
            int uiThread = OnUi(() => Environment.CurrentManagedThreadId);
            preferenceHandler = (_, _) =>
            {
                Interlocked.Increment(ref preferenceEvents);
                if (Environment.CurrentManagedThreadId != uiThread) Interlocked.Increment(ref wrongPreferenceThread);
            };
            OnUi(() => { preferences!.AccessibilityPreferencesChanged += preferenceHandler; return true; });
            stage = "links-and-number"; CheckLinksAndNumber(demo);
            stage = "dates"; CheckDates(demo);
            stage = "text-and-privacy"; CheckTextAndPrivacy(demo);
            stage = "grid"; CheckGrid(demo);
            stage = "viewport"; CheckViewport(demo);
            // Rebind the actual native host through Activity recreation and reject its old IDs.
            stage = "recreation";
            using var oldLink = FindLabel("P4 linked action");
            int previousPreferenceEvents = Volatile.Read(ref preferenceEvents);
            OnUi(() => { activity.Recreate(); return true; });
            using var rebound = WaitForRecreation(activity, sharedApp,
                () => Volatile.Read(ref preferenceEvents) > previousPreferenceEvents);
            Check(!oldLink.Refresh(), "recreation-old-native-id-retired");
            int count = OnUi(() => demo.LinkInvocations);
            Check(rebound.PerformAction(NativeAction.Click), "recreation-new-link-action");
            Check(OnUi(() => demo.LinkInvocations == count + 1), "recreation-shared-control-retained");
            var reboundPreferences = OnUi(ReadPreferences);
            Check(ReferenceEquals(preferences, reboundPreferences.Provider), "preferences-recreation-canonical-provider-retained");
            Check(reboundPreferences.Scale > 0 && reboundPreferences.Scale == reboundPreferences.ConfigurationScale,
                "preferences-recreation-current-configuration-scale");
            Check(OnUi(() => AndroidWindowKit.Current.ActivityTracker.CurrentActivity is { } current
                && !ReferenceEquals(current, activity)), "preferences-recreation-current-activity");
            Check(Volatile.Read(ref preferenceEvents) > previousPreferenceEvents && Volatile.Read(ref wrongPreferenceThread) == 0,
                "preferences-recreation-live-ui-subscription");
            success = true;
        }
        catch (Exception error)
        {
            // Exception messages and native node/Bundle dumps may include document payloads.
            // The last constant check category identifies assertion failures without exposing them.
            global::Android.Util.Log.Error("MFN.Accessibility.Phase4", $"FAIL stage={stage} exception={error.GetType().Name}");
        }
        finally
        {
            automation.AccessibilityEvent -= OnAccessibilityEvent;
            if (preferences is not null && preferenceHandler is not null)
            {
                try { OnUi(() => { preferences.AccessibilityPreferencesChanged -= preferenceHandler; return true; }); }
                catch (Exception error)
                {
                    success = false;
                    global::Android.Util.Log.Error("MFN.Accessibility.Phase4", $"FAIL stage=preferences-unsubscribe exception={error.GetType().Name}");
                }
            }
        }
        result.PutString("stream", $"ANDROID_ACCESSIBILITY_PHASE4_{(success ? "PASS" : "FAIL")} assertions={assertions}\n");
        Finish(success ? Result.Ok : Result.Canceled, result);
    }

    private void CheckLinksAndNumber(AccessibilityPhase4Panel demo)
    {
        using var link = FindLabel("P4 linked action");
        Check(link.Clickable && link.VisibleToUser, "link-native-action-and-bounds");
        Check(link.PerformAction(NativeAction.Click), "link-native-click");
        Check(OnUi(() => demo.LinkInvocations == 1), "link-normal-event");
        using var number = FindLabel("P4 Number", node => { using var info = node.GetRangeInfo(); return info is not null; });
        using var range = number.GetRangeInfo();
        Check(range is not null && range.Min == 0 && range.Max == 10 && range.Current == 2, "numeric-native-range");
        using var value = new Bundle();
        value.PutFloat("android.view.accessibility.action.ARGUMENT_PROGRESS_VALUE", 4.5f);
        Check(number.PerformAction((NativeAction)16908349, value), "numeric-native-write-with-readonly-editor");
        Check(OnUi(() => demo.Number.Value == 4.5m && demo.Number.ReadOnly), "numeric-canonical-decimal-value");
        using var up = Child(number, 1);
        Check(up.PerformAction(NativeAction.Click), "numeric-logical-button");
        Check(OnUi(() => demo.Number.Value == 5m), "numeric-existing-step");
    }

    private void CheckDates(AccessibilityPhase4Panel demo)
    {
        using var date = FindLabel("P4 Date");
        using var toggle = Child(date, 0);
        using var up = Child(date, 1);
        Check(toggle.Checkable && IsChecked(toggle), "date-checkbox-metadata");
        Check(toggle.PerformAction(NativeAction.Click), "date-checkbox-toggle");
        Check(OnUi(() => !demo.Date.Checked), "date-unchecked-canonical-state");
        Check(!up.PerformAction(NativeAction.Click), "date-unchecked-step-rejected");
        Check(toggle.PerformAction(NativeAction.Click), "date-checkbox-reenable");
        var previous = OnUi(() => demo.Date.Value);
        Check(up.PerformAction(NativeAction.Click), "date-updown-native-action");
        Check(OnUi(() => demo.Date.Value != previous), "date-normal-step-path");
        using var windowless = FindLabel("P4 Windowless date");
        Check(!windowless.PerformAction(NativeAction.Expand), "date-windowless-expand-unavailable");
        using var calendar = Child(windowless, 0);
        Check(!calendar.PerformAction(NativeAction.Click), "date-windowless-calendar-unavailable");
        Check(OnUi(() => (demo.WindowlessDate.AccessibilityObject.State
            & (ModernFormsNext.Accessibility.AccessibleStates.HasPopup | ModernFormsNext.Accessibility.AccessibleStates.Expanded)) == 0), "date-no-popup-capability-or-expanded-state");
    }

    private void CheckTextAndPrivacy(AccessibilityPhase4Panel demo)
    {
        using var editor = FindLabel("P4 Rich editor");
        using var selection = new Bundle();
        selection.PutInt("ACTION_ARGUMENT_SELECTION_START_INT", 0);
        selection.PutInt("ACTION_ARGUMENT_SELECTION_END_INT", 5);
        Check(editor.PerformAction(NativeAction.SetSelection, selection), "text-native-selection");
        Check(editor.Refresh() && editor.TextSelectionStart == 0 && editor.TextSelectionEnd == 5, "text-selection-metadata");
        Check(OnUi(() => demo.Editor.SelectionStart == 0 && demo.Editor.SelectionEnd == 5), "text-canonical-selection");
        selection.PutInt("ACTION_ARGUMENT_SELECTION_START_INT", 6);
        selection.PutInt("ACTION_ARGUMENT_SELECTION_END_INT", 6);
        Check(editor.PerformAction(NativeAction.SetSelection, selection), "text-emoji-start");
        using var movement = new Bundle();
        movement.PutInt("ACTION_ARGUMENT_MOVEMENT_GRANULARITY_INT", 1);
        movement.PutBoolean("ACTION_ARGUMENT_EXTEND_SELECTION_BOOLEAN", false);
        Check(editor.PerformAction(NativeAction.NextAtMovementGranularity, movement), "text-character-granularity");
        Check(editor.Refresh() && editor.TextSelectionStart == 8 && editor.TextSelectionEnd == 8, "text-utf16-surrogate-boundary");
        using var bounds = new global::Android.Graphics.Rect();
        editor.GetBoundsInScreen(bounds);
        Check(bounds.Width() > 0 && bounds.Height() > 0 && editor.VisibleToUser, "text-native-control-geometry");
        Check(OnUi(() => demo.Editor.AccessibilityObject.TextProvider!.RangeFromOffsets(0, 5).GetBoundingRectangles()
            .Any(rect => rect.Width > 0 && rect.Height > 0)), "text-actual-renderer-range-geometry");
        using var password = FindLabel("P4 Password");
        Check(password.Password && string.IsNullOrEmpty(password.Text), "password-native-value-redacted");
        Check(OperatingSystem.IsAndroidVersionAtLeast(26) && password.HintText == "P4 Password", "password-authored-label-retained");
        Check(!password.PerformAction(NativeAction.SetSelection, selection), "password-text-pattern-rejected");
        string privateText = OnUi(() => demo.PrivateFixtureValue) + "x";
        using var value = new Bundle();
        value.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", privateText);
        Check(password.PerformAction(NativeAction.SetText, value), "password-native-write");
        using var group = FindLabel("P4 Protected group");
        using var protectedChild = Child(group, 0);
        Check(protectedChild.Password && string.IsNullOrEmpty(protectedChild.Text)
            && string.IsNullOrEmpty(protectedChild.ContentDescription), "protected-descendant-text-and-name-redacted");
        using var extras = protectedChild.Extras;
        Check(string.IsNullOrEmpty(extras?.GetString("ModernFormsNext.Help"))
            && string.IsNullOrEmpty(extras?.GetString("ModernFormsNext.AutomationId")), "protected-descendant-extras-redacted");
        // Native assistive technology retains ordinary interaction. Sensitivity is payload
        // redaction; AutomationSession applies its own, separately documented action policy.
        Check(protectedChild.PerformAction(NativeAction.Click)
            && OnUi(() => demo.ProtectedInvocations == 1), "protected-descendant-normal-button-activation");
        using var protectedEditor = Child(group, 1);
        Check(protectedEditor.Enabled && protectedEditor.VisibleToUser && protectedEditor.Password
            && string.IsNullOrEmpty(protectedEditor.Text)
            && string.IsNullOrEmpty(protectedEditor.ContentDescription), "protected-editor-native-payload-redacted");
        selection.PutInt("ACTION_ARGUMENT_SELECTION_START_INT", 0);
        selection.PutInt("ACTION_ARGUMENT_SELECTION_END_INT", 5);
        var protectedSelection = OnUi(() => (demo.ProtectedEditor.SelectionStart, demo.ProtectedEditor.SelectionEnd));
        Check(!protectedEditor.PerformAction(NativeAction.SetSelection, selection)
            && !protectedEditor.PerformAction(NativeAction.NextAtMovementGranularity, movement)
            && OnUi(() => (demo.ProtectedEditor.SelectionStart, demo.ProtectedEditor.SelectionEnd) == protectedSelection),
            "protected-editor-selection-and-movement-rejected");
        using var protectedNumber = Child(group, 2);
        using var protectedRange = protectedNumber.GetRangeInfo();
        Check(protectedNumber.Enabled && protectedNumber.VisibleToUser && protectedNumber.Password
            && protectedRange is null && string.IsNullOrEmpty(protectedNumber.Text), "protected-number-range-redacted");
        using var progress = new Bundle();
        progress.PutFloat("android.view.accessibility.action.ARGUMENT_PROGRESS_VALUE", 4.5f);
        Check(!protectedNumber.PerformAction((NativeAction)16908349, progress)
            && OnUi(() => demo.ProtectedNumber.Value == 2), "protected-number-payload-write-rejected");
        using var root = UiAutomation!.RootInActiveWindow;
        var matches = root!.FindAccessibilityNodeInfosByText(privateText);
        try { Check(matches is null || matches.Count == 0, "password-native-search-redacted"); }
        finally { if (matches is not null) foreach (var node in matches) node.Dispose(); }
        Idle();
        Check(Volatile.Read(ref sensitiveEvents) > 0 && Volatile.Read(ref sensitivePayloads) == 0, "sensitive-native-event-payloads-redacted");
    }

    private void CheckGrid(AccessibilityPhase4Panel demo)
    {
        using var grid = FindLabel("P4 Grid");
        using var collection = grid.GetCollectionInfo();
        Check(collection?.RowCount == 20 && collection.ColumnCount == 2, "grid-native-dimensions");
        using var cell = FindCell(0, 0);
        using var coordinates = cell.GetCollectionItemInfo();
        Check(coordinates?.RowIndex == 0 && coordinates.ColumnIndex == 0, "grid-native-cell-coordinates");
        var originalRow = OnUi(() => demo.Grid.Rows[0]);
        using var edit = new Bundle();
        edit.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", "Edited");
        Check(cell.PerformAction(NativeAction.SetText, edit), "grid-native-cell-edit");
        Check(OnUi(() => originalRow.Cells[0].Value == "Edited"), "grid-normal-edit-commit");
        using var header = FindLabel("P4 Item", node => OperatingSystem.IsAndroidVersionAtLeast(28) && node.Heading);
        Check(header.PerformAction(NativeAction.Click), "grid-native-header-sort");
        Check(cell.Refresh(), "grid-sorted-row-identity-retained");
        using var sorted = cell.GetCollectionItemInfo();
        Check(sorted?.RowIndex == OnUi(() => originalRow.Index) && sorted.RowIndex != 0, "grid-sorted-coordinate-updated");
        using var distant = FindCell(18, 0);
        Check(distant.PerformAction((NativeAction)16908342), "grid-native-reveal-cell");
        Check(distant.Refresh() && distant.VisibleToUser, "grid-revealed-native-bounds");
        OnUi(() => { demo.Grid.Rows.Remove(originalRow); return true; });
        Check(!cell.Refresh() && !cell.PerformAction(NativeAction.SetText, edit), "grid-removed-native-cell-rejected");
    }

    private void CheckViewport(AccessibilityPhase4Panel demo)
    {
        using var viewport = FindLabel("P4 Viewport");
        int previous = OnUi(() => demo.Viewport.VerticalScrollProperties.Value);
        Check(viewport.Scrollable && viewport.PerformAction(NativeAction.ScrollForward), "viewport-native-page-scroll");
        Check(OnUi(() => demo.Viewport.VerticalScrollProperties.Value > previous), "viewport-canonical-offset");
        using var far = FindLabel("P4 Far action");
        Check(far.PerformAction((NativeAction)16908342), "viewport-native-show-on-screen");
        Check(far.Refresh() && far.VisibleToUser && far.PerformAction(NativeAction.Click), "viewport-revealed-control-action");
        Check(OnUi(() => demo.FarInvocations == 1), "viewport-real-control-click");
    }

    private AccessibilityNodeInfo FindCell(int row, int column) => Find(node =>
    {
        using var cell = node.GetCollectionItemInfo();
        return cell?.RowIndex == row && cell.ColumnIndex == column;
    });

    private static bool HasLabel(AccessibilityNodeInfo node, string label)
        => node.ContentDescription == label || node.Text == label
            || OperatingSystem.IsAndroidVersionAtLeast(26) && node.HintText == label;

    private AccessibilityNodeInfo FindLabel(string label, Func<AccessibilityNodeInfo, bool>? extra = null)
        => Find(node => HasLabel(node, label) && (extra?.Invoke(node) ?? true));

    private AccessibilityNodeInfo WaitForRecreation(MainActivity previous, App sharedApp, Func<bool> preferencesRefreshed)
        => Find(node => HasLabel(node, "P4 linked action"), () => OnUi(() =>
            AndroidWindowKit.Current.ActivityTracker.CurrentActivity is MainActivity current
            && !ReferenceEquals(current, previous)
            && current.Window?.DecorView?.IsAttachedToWindow == true
            && current.Application is SampleApplication application
            && ReferenceEquals(application.SharedApp, sharedApp)) && preferencesRefreshed(),
            Environment.TickCount64 + 5000);

    private AccessibilityNodeInfo Find(Func<AccessibilityNodeInfo, bool> predicate,
        Func<bool>? ready = null, long? deadline = null)
    {
        long until = deadline ?? Environment.TickCount64 + 2000;
        do
        {
            var pending = new Queue<AccessibilityNodeInfo>();
            if (ready?.Invoke() != false && UiAutomation!.RootInActiveWindow is { } root) pending.Enqueue(root);
            try
            {
                for (int visited = 0; pending.Count > 0 && visited < 1024; visited++)
                {
                    var node = pending.Dequeue();
                    bool retained = false;
                    try
                    {
                        if (predicate(node)) { retained = true; return node; }
                        for (int index = 0; index < node.ChildCount; index++)
                            if (node.GetChild(index) is { } child) pending.Enqueue(child);
                    }
                    finally { if (!retained) node.Dispose(); }
                }
            }
            finally { while (pending.TryDequeue(out var node)) node.Dispose(); }
            long remaining = until - Environment.TickCount64;
            if (remaining <= 0) break;
            // Poll native state from the instrumentation thread, never the UI thread. The
            // shared deadline covers Activity, preference notification and virtual-host readiness.
            Thread.Sleep((int)Math.Min(100, remaining));
        } while (Environment.TickCount64 < until);
        throw new InvalidOperationException("Required fixture node was unavailable.");
    }

    private static AccessibilityNodeInfo Child(AccessibilityNodeInfo node, int index)
        => node.GetChild(index) ?? throw new InvalidOperationException("Required fixture child was unavailable.");

    private static bool IsChecked(AccessibilityNodeInfo node)
        => OperatingSystem.IsAndroidVersionAtLeast(36) ? node.CheckedState == CheckedState.True : node.Checked;

    // Both sides are read on the Android UI thread. The provider resolves the current Activity;
    // the comparison does not cache native configuration or substitute a test settings service.
    private static (IPlatformAccessibilitySettings? Provider, double? Scale, double? ConfigurationScale) ReadPreferences()
    {
        var provider = AvaloniaGlobals.GetService<IPlatformSettings>() as IPlatformAccessibilitySettings;
        var activity = AndroidWindowKit.Current.ActivityTracker.CurrentActivity;
        return (provider, provider?.GetAccessibilityPreferences().TextScale, activity?.Resources?.Configuration?.FontScale);
    }

    private T OnUi<T>(Func<T> function)
    {
        T result = default!;
        Exception? failure = null;
        RunOnMainSync(() => { try { result = function(); } catch (Exception error) { failure = error; } });
        if (failure is not null) throw new InvalidOperationException("UI fixture operation failed.");
        return result;
    }

    private void Idle() { WaitForIdleSync(); UiAutomation!.WaitForIdle(100, 5000); }
    private void OnAccessibilityEvent(object? sender, UiAutomation.AccessibilityEventEventArgs e)
    {
        if (e.Event is not { Password: true } change || change.PackageName != Package) return;
        Interlocked.Increment(ref sensitiveEvents);
        if (change.Text?.Count > 0 || !string.IsNullOrEmpty(change.BeforeText) || !string.IsNullOrEmpty(change.ContentDescription))
            Interlocked.Increment(ref sensitivePayloads);
    }

    private void Check(bool condition, string category)
    {
        if (!condition)
        {
            global::Android.Util.Log.Error("MFN.Accessibility.Phase4", $"FAIL category={category}");
            throw new InvalidOperationException();
        }
        assertions++;
    }
}
