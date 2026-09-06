using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;
using NativeAction = Android.Views.Accessibility.Action;

namespace ModernFormsNext.CrossPlatform.Sample;

/// <summary>
/// Opt-in native accessibility integration checks over the real Android service connection.
/// Run with adb am instrument; the ordinary sample launch never executes this fixture.
/// </summary>
[Instrumentation(Name = "com.programajster.modernformsnext.sample.AccessibilityInstrumentation",
    TargetPackage = "com.programajster.modernformsnext.sample", FunctionalTest = true)]
public sealed class AccessibilityInstrumentation : Instrumentation
{
    private int assertions;

    /// <summary>Creates the explicit instrumentation runner.</summary>
    public AccessibilityInstrumentation() { }

    /// <summary>Reattaches the managed runner to Android's native instrumentation instance.</summary>
    public AccessibilityInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership) { }

    /// <inheritdoc/>
    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);
        Start();
    }

    /// <inheritdoc/>
    public override void OnStart()
    {
        base.OnStart();
        using var result = new Bundle();
        try
        {
            // Connect before creating the window so Android assigns service connection IDs to
            // subsequent node results and observes the initial window/focus notifications.
            var automation = UiAutomation!;
            var eventCounts = new System.Collections.Concurrent.ConcurrentDictionary<EventTypes, int>();
            int sensitiveEvents = 0, sensitivePayloads = 0;
            automation.AccessibilityEvent += (_, e) =>
            {
                if (e.Event is not { } change || change.PackageName != "com.programajster.modernformsnext.sample") return;
                eventCounts.AddOrUpdate(change.EventType, 1, (_, count) => count + 1);
                if (change.Password)
                {
                    Interlocked.Increment(ref sensitiveEvents);
                    if (change.Text?.Count > 0 || !string.IsNullOrEmpty(change.BeforeText)
                        || !string.IsNullOrEmpty(change.ContentDescription)) Interlocked.Increment(ref sensitivePayloads);
                }
            };
            using var intent = new Intent(TargetContext!, typeof(MainActivity));
            intent.AddFlags(ActivityFlags.NewTask);
            intent.PutExtra("ACCESSIBILITY_DEMO", true);
            var activity = (MainActivity)StartActivitySync(intent)!;
            WaitForIdleSync();
            automation.WaitForIdle(100, 5000);
            Thread.Sleep(300);
            var demo = ((SampleApplication)activity.Application!).SharedApp.Root.Controls.OfType<AccessibilityDemoPanel>().Single();
            using var root = UiAutomation!.RootInActiveWindow;
            Check(root is not null, "native window root");
            using var button = Find("Invoke sample");
            Check(button.ClassName == "android.widget.Button", "button class");
            Check(button.PackageName == "com.programajster.modernformsnext.sample", "package");
            Check(button.VisibleToUser, "button visible");
            Check(button.PerformAction(NativeAction.Click), "invoke action");
            RunOnMainSync(() => Check(demo.Invocations == 1, "normal click event"));
            Check(button.PerformAction(NativeAction.Focus), "input focus action");
            using var focused = UiAutomation.FindFocus(NodeFocus.Input);
            Check(focused?.ContentDescription == "Invoke sample", "input focus lookup");
            using var toggle = Find("Check sample");
            Check(toggle.Checkable, "checkable");
            Check(toggle.PerformAction(NativeAction.Click), "toggle action");
            RunOnMainSync(() => Check(demo.Check.Checked, "normal toggle state"));
            Check(toggle.PerformAction(NativeAction.AccessibilityFocus), "accessibility focus action");
            using var accessibilityFocus = UiAutomation.FindFocus(NodeFocus.Accessibility);
            Check(accessibilityFocus?.ContentDescription == "Check sample", "accessibility focus lookup");
            RunOnMainSync(() => Check(demo.InvokeButton.Focused, "input focus remains independent"));
            using var edit = Find("Editor sample");
            Check(edit.Text == "Initial text" && (OperatingSystem.IsAndroidVersionAtLeast(26)
                ? edit.HintText == "Editor sample" : edit.ContentDescription == "Editor sample"), "editor label and value");
            using var text = new Bundle();
            text.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", "Updated through accessibility");
            Check(edit.PerformAction(NativeAction.SetText, text), "set text action");
            RunOnMainSync(() => Check(demo.Editor.Text == "Updated through accessibility", "framework text state"));
            using var password = Find("Password sample");
            string secret = Guid.NewGuid().ToString();
            text.PutCharSequence("ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE", secret);
            Check(password.PerformAction(NativeAction.SetText, text), "password write");
            using var redacted = Find("Password sample");
            Check(redacted.Password && string.IsNullOrEmpty(redacted.Text), "password redacted");
            Check(string.IsNullOrEmpty(redacted.ContentDescription), "password content description");
            using var secretMatches = UiAutomation.RootInActiveWindow;
            var matches = secretMatches!.FindAccessibilityNodeInfosByText(secret)!;
            Check(matches.Count == 0, "password excluded from text search");
            foreach (var match in matches) match.Dispose();
            bool passwordType = false, passwordLearning = false, ordinaryType = false;
            RunOnMainSync(() =>
            {
                var content = activity.FindViewById<global::Android.Views.ViewGroup>(global::Android.Resource.Id.Content)!;
                var view = (ModernFormsNext.WindowKit.Backend.Android.Rendering.AndroidSkiaHostView)content.GetChildAt(0)!;
                demo.Password.Select();
                using var passwordInfo = new global::Android.Views.InputMethods.EditorInfo();
                // The view owns its active input connection; do not dispose its borrowed result.
                view.OnCreateInputConnection(passwordInfo);
                var privateFlags = global::Android.Text.InputTypes.TextVariationPassword | global::Android.Text.InputTypes.TextFlagNoSuggestions;
                passwordType = (passwordInfo.InputType & privateFlags) == privateFlags;
                passwordLearning = !OperatingSystem.IsAndroidVersionAtLeast(26)
                    || (passwordInfo.ImeOptions & global::Android.Views.InputMethods.ImeFlags.NoPersonalizedLearning) != 0;
                demo.Editor.Select();
                using var editorInfo = new global::Android.Views.InputMethods.EditorInfo();
                view.OnCreateInputConnection(editorInfo);
                ordinaryType = (editorInfo.InputType & privateFlags) == 0;
            });
            // Assert on the runner thread so a failure is reported without crashing Android's UI.
            Check(passwordType, "password IME type and suggestions");
            Check(passwordLearning, "password IME personalized learning");
            Check(ordinaryType, "ordinary editor resets password IME flags");
            using var slider = Find("Slider sample");
            using var range = slider.GetRangeInfo();
            Check(range is not null && range.Min == 0 && range.Max == 100 && range.Current == 25, "range metadata");
            using var progress = new Bundle();
            progress.PutFloat("android.view.accessibility.action.ARGUMENT_PROGRESS_VALUE", 60);
            Check(slider.PerformAction((NativeAction)16908349, progress), "set progress");
            RunOnMainSync(() => Check(demo.Slider.Value == 60, "framework range state"));
            using var readOnly = Find("Progress sample");
            Check(!readOnly.PerformAction((NativeAction)16908349, progress), "read only progress rejects write");
            using var list = Find("List sample");
            using var collection = list.GetCollectionInfo();
            Check(collection?.RowCount == 2, "list collection");
            using var item = Find("Second item");
            Check(item.PerformAction(NativeAction.Select), "logical selection");
            RunOnMainSync(() => Check(demo.List.SelectedIndex == 1, "framework selection state"));
            using var branch = Find("Branch item");
            Check(branch.PerformAction(NativeAction.Expand), "tree expansion");
            using var leaf = Find("Leaf item");
            Check(leaf.Parent is not null, "logical child parent");
            using var custom = Find("Logical action");
            Check(custom.PerformAction(NativeAction.Click), "custom semantic child invoke");
            using var disabled = Find("Disabled sample");
            Check(!disabled.Enabled && !disabled.PerformAction(NativeAction.Click), "disabled action rejected");
            using var dynamicButton = Find("Add or remove item");
            Check(dynamicButton.PerformAction(NativeAction.Click), "dynamic add");
            using var added = Find("Dynamic item");
            Check(dynamicButton.PerformAction(NativeAction.Click), "dynamic remove");
            Thread.Sleep(150);
            Check(!added.Refresh() && !added.PerformAction(NativeAction.Select), "stale native node rejected");
            using var bounds = new global::Android.Graphics.Rect();
            button.GetBoundsInScreen(bounds);
            Check(bounds.Width() > 0 && bounds.Height() > 0, "physical bounds");
            Thread.Sleep(200);
            Check(eventCounts.ContainsKey(EventTypes.ViewClicked), "native click event");
            Check(eventCounts.ContainsKey(EventTypes.ViewFocused), "native input focus event");
            Check(eventCounts.ContainsKey(EventTypes.ViewAccessibilityFocused), "native accessibility focus event");
            Check(eventCounts.ContainsKey(EventTypes.ViewSelected), "native selection event");
            Check(eventCounts.ContainsKey(EventTypes.WindowContentChanged), "native content event");
            Check(sensitiveEvents > 0 && sensitivePayloads == 0, "sensitive native event payloads");
            RunOnMainSync(() =>
            {
                var content = activity.FindViewById<global::Android.Views.ViewGroup>(global::Android.Resource.Id.Content)!;
                var view = (ModernFormsNext.WindowKit.Backend.Android.Rendering.AndroidSkiaHostView)content.GetChildAt(0)!;
                var semantics = view.AccessibilityHost;
                view.AccessibilityHost = null;
                view.AccessibilityHost = semantics;
            });
            Thread.Sleep(150);
            Check(!button.Refresh(), "same View replacement rejects previous IDs");
            using var rebound = Find("Invoke sample");
            Check(rebound.PerformAction(NativeAction.Click), "same View replacement action");
            // Recreate through Android's real activity lifecycle, preserving the process tree.
            RunOnMainSync(activity.Recreate);
            Thread.Sleep(1000);
            WaitForIdleSync();
            using var recreated = Find("Invoke sample");
            Check(recreated.PerformAction(NativeAction.Click), "recreated host action");
            Check(!rebound.Refresh(), "old host native identity rejected");
            result.PutString("stream", $"ANDROID_ACCESSIBILITY_PASS assertions={assertions}\n");
            Finish(Result.Ok, result);
        }
        catch (Exception)
        {
            // Never print exception messages, bundles, nodes, or editor contents.
            result.PutString("stream", $"ANDROID_ACCESSIBILITY_FAIL completed_assertions={assertions}\n");
            Finish(Result.Canceled, result);
        }
    }

    private AccessibilityNodeInfo Find(string label)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            using var root = UiAutomation!.RootInActiveWindow;
            var matches = root?.FindAccessibilityNodeInfosByText(label);
            AccessibilityNodeInfo? found = null;
            if (matches is not null)
            {
                foreach (var node in matches)
                {
                    if (found is null && (node.ContentDescription == label || node.Text == label
                        || OperatingSystem.IsAndroidVersionAtLeast(26) && node.HintText == label)) found = node;
                    else node.Dispose();
                }
            }
            if (found is not null) return found;
            Thread.Sleep(100);
        }
        throw new InvalidOperationException();
    }

    private void Check(bool condition, string category)
    {
        if (!condition)
        {
            global::Android.Util.Log.Error("MFN.Accessibility", $"FAIL: {category}");
            throw new InvalidOperationException();
        }
        assertions++;
    }
}
