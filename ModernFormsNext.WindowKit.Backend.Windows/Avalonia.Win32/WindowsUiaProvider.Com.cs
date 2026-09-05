using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

internal partial class WindowsUiaProvider
{
    public int AbiGetProviderOptions(out ProviderOptions value)
        => TryGet(() => ProviderOptions, out value);

    public int AbiGetPatternProvider(int patternId, out IntPtr provider)
        => TryGet(
            () => GetPatternProvider(patternId) is null
                ? IntPtr.Zero
                : WindowsUiaNativeMethods.GetUnknownPointer(this),
            out provider);

    public int AbiGetPropertyValue(int propertyId, out WindowsUiaVariant value)
        => TryGet(
            () => WindowsUiaVariant.FromObject(GetPropertyValue(propertyId)),
            out value);

    public int AbiGetHostRawElementProvider(out IntPtr provider)
        => TryGet(
            () => IsRoot
                ? WindowsUiaNativeMethods.HostProviderFromHwnd(Context.Hwnd)
                : IntPtr.Zero,
            out provider);

    public int AbiNavigate(NavigateDirection direction, out IntPtr provider)
        => TryGet(
            () => Navigate(direction) is WindowsUiaProvider target
                ? WindowsUiaNativeMethods.GetFragmentProviderPointer(target)
                : IntPtr.Zero,
            out provider);

    public int AbiGetRuntimeId(out IntPtr runtimeId)
        => TryGet(
            () => WindowsUiaNativeMethods.CreateSafeArray(GetRuntimeId()),
            out runtimeId);

    public int AbiGetBoundingRectangle(out UiaRect bounds)
        => TryGet(() => BoundingRectangle, out bounds);

    public int AbiGetEmbeddedFragmentRoots(out IntPtr roots)
    {
        roots = IntPtr.Zero;
        return 0;
    }

    public int AbiSetFocus()
        => Try(SetFocus);

    public int AbiGetFragmentRoot(out IntPtr provider)
        => TryGet(
            () => WindowsUiaNativeMethods.GetFragmentRootProviderPointer(Context.Root),
            out provider);

    public int AbiInvoke()
        => Try(() => ((IInvokeProvider)this).Invoke());

    public int AbiToggle()
        => Try(() => ((IToggleProvider)this).Toggle());

    public int AbiGetToggleState(out ToggleState value)
        => TryGet(() => ToggleState, out value);

    public int AbiSetValue(IntPtr value)
        => Try(() => ((IValueProvider)this).SetValue(Marshal.PtrToStringUni(value) ?? string.Empty));

    public int AbiGetValue(out IntPtr value)
        => TryGet(
            () => Marshal.StringToBSTR(((IValueProvider)this).Value),
            out value);

    public int AbiGetIsReadOnly(out int value)
        => TryGet(() => ((IValueProvider)this).IsReadOnly ? 1 : 0, out value);

    int IRangeValueProviderAbi.AbiSetValue(double value)
        => Try(() => ((IRangeValueProvider)this).SetValue(value));

    int IRangeValueProviderAbi.AbiGetValue(out double value)
        => TryGet(() => ((IRangeValueProvider)this).Value, out value);

    int IRangeValueProviderAbi.AbiGetIsReadOnly(out int value)
        => TryGet(() => ((IRangeValueProvider)this).IsReadOnly ? 1 : 0, out value);

    public int AbiGetMaximum(out double value)
        => TryGet(() => ((IRangeValueProvider)this).Maximum, out value);

    public int AbiGetMinimum(out double value)
        => TryGet(() => ((IRangeValueProvider)this).Minimum, out value);

    public int AbiGetLargeChange(out double value)
        => TryGet(() => ((IRangeValueProvider)this).LargeChange, out value);

    public int AbiGetSmallChange(out double value)
        => TryGet(() => ((IRangeValueProvider)this).SmallChange, out value);

    public int AbiExpand()
        => Try(() => ((IExpandCollapseProvider)this).Expand());

    public int AbiCollapse()
        => Try(() => ((IExpandCollapseProvider)this).Collapse());

    public int AbiGetExpandCollapseState(out ExpandCollapseState value)
        => TryGet(() => ExpandCollapseState, out value);

    public int AbiGetSelection(out IntPtr providers)
        => TryGet(
            () => WindowsUiaNativeMethods.CreateSafeArray(
                ((ISelectionProvider)this).GetSelection()
                    .Cast<WindowsUiaProvider>()
                    .ToArray()),
            out providers);

    public int AbiGetCanSelectMultiple(out int value)
        => TryGet(() => ((ISelectionProvider)this).CanSelectMultiple ? 1 : 0, out value);

    public int AbiGetIsSelectionRequired(out int value)
        => TryGet(() => ((ISelectionProvider)this).IsSelectionRequired ? 1 : 0, out value);

    public int AbiSelect()
        => Try(() => ((ISelectionItemProvider)this).Select());

    public int AbiAddToSelection()
        => Try(() => ((ISelectionItemProvider)this).AddToSelection());

    public int AbiRemoveFromSelection()
        => Try(() => ((ISelectionItemProvider)this).RemoveFromSelection());

    public int AbiGetIsSelected(out int value)
        => TryGet(() => ((ISelectionItemProvider)this).IsSelected ? 1 : 0, out value);

    public int AbiGetSelectionContainer(out IntPtr provider)
        => TryGet(
            () => ((ISelectionItemProvider)this).SelectionContainer is WindowsUiaProvider target
                ? WindowsUiaNativeMethods.GetUnknownPointer(target)
                : IntPtr.Zero,
            out provider);

    public int AbiScrollIntoView()
        => Try(() => ((IScrollItemProvider)this).ScrollIntoView());

    protected static int Try(Action callback)
    {
        try
        {
            callback();
            return 0;
        }
        catch (Exception exception)
        {
            // Values are deliberately excluded because callbacks can handle passwords and other
            // user-entered content. The exception type retains diagnostic value without leakage.
            Trace.TraceError("ModernFormsNext UIA callback failed: {0}", exception.GetType().Name);
            return exception.HResult;
        }
    }

    protected static int TryGet<T>(Func<T> callback, out T value)
    {
        try
        {
            value = callback();
            return 0;
        }
        catch (Exception exception)
        {
            value = default!;
            Trace.TraceError("ModernFormsNext UIA callback failed: {0}", exception.GetType().Name);
            return exception.HResult;
        }
    }
}

internal sealed partial class WindowsUiaRootProvider
{
    public int AbiElementProviderFromPoint(double x, double y, out IntPtr provider)
        => TryGet(
            () => ElementProviderFromPoint(x, y) is WindowsUiaProvider target
                ? WindowsUiaNativeMethods.GetFragmentProviderPointer(target)
                : IntPtr.Zero,
            out provider);

    public int AbiGetFocus(out IntPtr provider)
        => TryGet(
            () => GetFocus() is WindowsUiaProvider target
                ? WindowsUiaNativeMethods.GetFragmentProviderPointer(target)
                : IntPtr.Zero,
            out provider);
}
