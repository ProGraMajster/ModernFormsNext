using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32;

// These declarations mirror the provider-side interfaces in UIAutomationCore.h. Keeping the
// native boundary local to the Windows backend avoids imposing WindowsDesktop/WPF/WinForms
// reference assemblies on the platform-neutral framework graph.

internal interface IRawElementProviderSimple
{
    ProviderOptions ProviderOptions { get; }

    [return: MarshalAs(UnmanagedType.Interface)]
    object? GetPatternProvider(int patternId);

    [return: MarshalAs(UnmanagedType.Struct)]
    object? GetPropertyValue(int propertyId);

    IRawElementProviderSimple? HostRawElementProvider { get; }
}

internal interface IRawElementProviderFragment
{
    IRawElementProviderFragment? Navigate(NavigateDirection direction);

    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_I4)]
    int[] GetRuntimeId();

    UiaRect BoundingRectangle { get; }

    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UNKNOWN)]
    IRawElementProviderSimple[]? GetEmbeddedFragmentRoots();

    void SetFocus();

    IRawElementProviderFragmentRoot FragmentRoot { get; }
}

internal interface IRawElementProviderFragmentRoot
{
    IRawElementProviderFragment? ElementProviderFromPoint(double x, double y);

    IRawElementProviderFragment? GetFocus();
}

internal interface IInvokeProvider
{
    void Invoke();
}

internal interface IToggleProvider
{
    void Toggle();

    ToggleState ToggleState { get; }
}

internal interface IValueProvider
{
    void SetValue([MarshalAs(UnmanagedType.LPWStr)] string value);

    string Value { get; }

    bool IsReadOnly
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        get;
    }
}

internal interface IRangeValueProvider
{
    void SetValue(double value);

    double Value { get; }

    bool IsReadOnly
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        get;
    }

    double Maximum { get; }

    double Minimum { get; }

    double LargeChange { get; }

    double SmallChange { get; }
}

internal interface IExpandCollapseProvider
{
    void Expand();

    void Collapse();

    ExpandCollapseState ExpandCollapseState { get; }
}

internal interface ISelectionProvider
{
    [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_UNKNOWN)]
    IRawElementProviderSimple[] GetSelection();

    bool CanSelectMultiple
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        get;
    }

    bool IsSelectionRequired
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        get;
    }
}

internal interface ISelectionItemProvider
{
    void Select();

    void AddToSelection();

    void RemoveFromSelection();

    bool IsSelected
    {
        [return: MarshalAs(UnmanagedType.Bool)]
        get;
    }

    IRawElementProviderSimple? SelectionContainer { get; }
}

internal interface IScrollItemProvider
{
    void ScrollIntoView();
}

// ABI-shaped interfaces use only blittable values and native pointers. This keeps the source-
// generated COM boundary independent of runtime marshalling and makes the HRESULT behavior of
// every callback explicit.

[GeneratedComInterface]
[Guid("D6DD68D1-86FD-4332-8666-9ABEDEA2D24C")]
internal partial interface IRawElementProviderSimpleAbi
{
    [PreserveSig] int AbiGetProviderOptions(out ProviderOptions value);
    [PreserveSig] int AbiGetPatternProvider(int patternId, out IntPtr provider);
    [PreserveSig] int AbiGetPropertyValue(int propertyId, out WindowsUiaVariant value);
    [PreserveSig] int AbiGetHostRawElementProvider(out IntPtr provider);
}

[GeneratedComInterface]
[Guid("F7063DA8-8359-439C-9297-BBC5299A7D87")]
internal partial interface IRawElementProviderFragmentAbi
{
    [PreserveSig] int AbiNavigate(NavigateDirection direction, out IntPtr provider);
    [PreserveSig] int AbiGetRuntimeId(out IntPtr runtimeId);
    [PreserveSig] int AbiGetBoundingRectangle(out UiaRect bounds);
    [PreserveSig] int AbiGetEmbeddedFragmentRoots(out IntPtr roots);
    [PreserveSig] int AbiSetFocus();
    [PreserveSig] int AbiGetFragmentRoot(out IntPtr provider);
}

[GeneratedComInterface]
[Guid("620CE2A5-AB8F-40A9-86CB-DE3C75599B58")]
internal partial interface IRawElementProviderFragmentRootAbi
{
    [PreserveSig] int AbiElementProviderFromPoint(double x, double y, out IntPtr provider);
    [PreserveSig] int AbiGetFocus(out IntPtr provider);
}

[GeneratedComInterface]
[Guid("54FCB24B-E18E-47A2-B4D3-ECCBE77599A2")]
internal partial interface IInvokeProviderAbi
{
    [PreserveSig] int AbiInvoke();
}

[GeneratedComInterface]
[Guid("56D00BD0-C4F4-433C-A836-1A52A57E0892")]
internal partial interface IToggleProviderAbi
{
    [PreserveSig] int AbiToggle();
    [PreserveSig] int AbiGetToggleState(out ToggleState value);
}

[GeneratedComInterface]
[Guid("C7935180-6FB3-4201-B174-7DF73ADBF64A")]
internal partial interface IValueProviderAbi
{
    [PreserveSig] int AbiSetValue(IntPtr value);
    [PreserveSig] int AbiGetValue(out IntPtr value);
    [PreserveSig] int AbiGetIsReadOnly(out int value);
}

[GeneratedComInterface]
[Guid("36DC7AEF-33E6-4691-AFE1-2BE7274B3D33")]
internal partial interface IRangeValueProviderAbi
{
    [PreserveSig] int AbiSetValue(double value);
    [PreserveSig] int AbiGetValue(out double value);
    [PreserveSig] int AbiGetIsReadOnly(out int value);
    [PreserveSig] int AbiGetMaximum(out double value);
    [PreserveSig] int AbiGetMinimum(out double value);
    [PreserveSig] int AbiGetLargeChange(out double value);
    [PreserveSig] int AbiGetSmallChange(out double value);
}

[GeneratedComInterface]
[Guid("D847D3A5-CAB0-4A98-8C32-ECB45C59AD24")]
internal partial interface IExpandCollapseProviderAbi
{
    [PreserveSig] int AbiExpand();
    [PreserveSig] int AbiCollapse();
    [PreserveSig] int AbiGetExpandCollapseState(out ExpandCollapseState value);
}

[GeneratedComInterface]
[Guid("FB8B03AF-3BDF-48D4-BD36-1A65793BE168")]
internal partial interface ISelectionProviderAbi
{
    [PreserveSig] int AbiGetSelection(out IntPtr providers);
    [PreserveSig] int AbiGetCanSelectMultiple(out int value);
    [PreserveSig] int AbiGetIsSelectionRequired(out int value);
}

[GeneratedComInterface]
[Guid("2ACAD808-B2D4-452D-A407-91FF1AD167B2")]
internal partial interface ISelectionItemProviderAbi
{
    [PreserveSig] int AbiSelect();
    [PreserveSig] int AbiAddToSelection();
    [PreserveSig] int AbiRemoveFromSelection();
    [PreserveSig] int AbiGetIsSelected(out int value);
    [PreserveSig] int AbiGetSelectionContainer(out IntPtr provider);
}

[GeneratedComInterface]
[Guid("2360C714-4BF1-4B26-BA65-9B21316127EB")]
internal partial interface IScrollItemProviderAbi
{
    [PreserveSig] int AbiScrollIntoView();
}

[Flags]
internal enum ProviderOptions
{
    ClientSideProvider = 0x1,
    ServerSideProvider = 0x2,
    NonClientAreaProvider = 0x4,
    OverrideProvider = 0x8,
    ProviderOwnsSetFocus = 0x10,
    UseComThreading = 0x20
}

internal enum NavigateDirection
{
    Parent,
    NextSibling,
    PreviousSibling,
    FirstChild,
    LastChild
}

internal enum ToggleState
{
    Off,
    On,
    Indeterminate
}

internal enum ExpandCollapseState
{
    Collapsed,
    Expanded,
    PartiallyExpanded,
    LeafNode
}

internal enum StructureChangeType
{
    ChildAdded,
    ChildRemoved,
    ChildrenInvalidated,
    ChildrenBulkAdded,
    ChildrenBulkRemoved,
    ChildrenReordered
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct UiaRect : IEquatable<UiaRect>
{
    public UiaRect(double left, double top, double width, double height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public double Left { get; }

    public double Top { get; }

    public double Width { get; }

    public double Height { get; }

    public bool Equals(UiaRect other)
        => Left.Equals(other.Left)
            && Top.Equals(other.Top)
            && Width.Equals(other.Width)
            && Height.Equals(other.Height);

    public override bool Equals(object? obj) => obj is UiaRect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Left, Top, Width, Height);
}

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct WindowsUiaVariant : IDisposable
{
    [FieldOffset(0)]
    private ushort variantType;

    [FieldOffset(8)]
    private short booleanValue;

    [FieldOffset(8)]
    private int integerValue;

    [FieldOffset(8)]
    private double doubleValue;

    [FieldOffset(8)]
    private IntPtr pointerValue;

    public static WindowsUiaVariant FromObject(object? value)
        => value switch
        {
            null => default,
            string text => new WindowsUiaVariant
            {
                variantType = (ushort)VarEnum.VT_BSTR,
                pointerValue = Marshal.StringToBSTR(text)
            },
            bool flag => new WindowsUiaVariant
            {
                variantType = (ushort)VarEnum.VT_BOOL,
                booleanValue = flag ? (short)-1 : (short)0
            },
            int number => new WindowsUiaVariant
            {
                variantType = (ushort)VarEnum.VT_I4,
                integerValue = number
            },
            double number => new WindowsUiaVariant
            {
                variantType = (ushort)VarEnum.VT_R8,
                doubleValue = number
            },
            UiaRect bounds => new WindowsUiaVariant
            {
                variantType = (ushort)(VarEnum.VT_ARRAY | VarEnum.VT_R8),
                pointerValue = WindowsUiaNativeMethods.CreateSafeArray(
                    [bounds.Left, bounds.Top, bounds.Width, bounds.Height])
            },
            _ => throw new NotSupportedException(
                $"The value type {value.GetType().Name} cannot be returned through UI Automation.")
        };

    public void Dispose()
    {
        if (variantType != (ushort)VarEnum.VT_EMPTY)
            _ = WindowsUiaNativeMethods.ClearVariant(ref this);
    }
}

internal static class WindowsUiaIds
{
    public const int RootObject = -25;
    public const int AppendRuntimeId = 3;

    public const int InvokePattern = 10000;
    public const int SelectionPattern = 10001;
    public const int ValuePattern = 10002;
    public const int RangeValuePattern = 10003;
    public const int ExpandCollapsePattern = 10005;
    public const int SelectionItemPattern = 10010;
    public const int TogglePattern = 10015;
    public const int ScrollItemPattern = 10017;

    public const int BoundingRectangleProperty = 30001;
    public const int ControlTypeProperty = 30003;
    public const int NameProperty = 30005;
    public const int HasKeyboardFocusProperty = 30008;
    public const int IsKeyboardFocusableProperty = 30009;
    public const int IsEnabledProperty = 30010;
    public const int AutomationIdProperty = 30011;
    public const int ClassNameProperty = 30012;
    public const int HelpTextProperty = 30013;
    public const int IsControlElementProperty = 30016;
    public const int IsContentElementProperty = 30017;
    public const int IsPasswordProperty = 30019;
    public const int IsOffscreenProperty = 30022;
    public const int FrameworkIdProperty = 30024;
    public const int ValueProperty = 30045;
    public const int RangeValueProperty = 30047;
    public const int ExpandCollapseStateProperty = 30070;
    public const int SelectionItemIsSelectedProperty = 30079;
    public const int ToggleStateProperty = 30086;

    public const int AutomationFocusChangedEvent = 20005;
    public const int ElementAddedToSelectionEvent = 20010;
    public const int ElementRemovedFromSelectionEvent = 20011;
    public const int ElementSelectedEvent = 20012;
    public const int SelectionInvalidatedEvent = 20013;
}

internal sealed class WindowsUiaElementNotAvailableException : COMException
{
    private const int ErrorElementNotAvailable = unchecked((int)0x80040201);

    public WindowsUiaElementNotAvailableException(string message)
        : base(message, ErrorElementNotAvailable)
    {
    }

    public WindowsUiaElementNotAvailableException(string message, Exception innerException)
        : base(message, innerException)
    {
        HResult = ErrorElementNotAvailable;
    }
}

internal sealed class WindowsUiaElementNotEnabledException : COMException
{
    private const int ErrorElementNotEnabled = unchecked((int)0x80040200);

    public WindowsUiaElementNotEnabledException()
        : base("The UI Automation element is not enabled.", ErrorElementNotEnabled)
    {
    }
}

internal sealed class WindowsUiaAccessDeniedException : COMException
{
    private const int ErrorAccessDenied = unchecked((int)0x80070005);

    public WindowsUiaAccessDeniedException()
        : base("The value of a password element is not available.", ErrorAccessDenied)
    {
    }
}

internal static class WindowsUiaNativeMethods
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();
    private static readonly Guid SimpleProviderInterfaceId = typeof(IRawElementProviderSimpleAbi).GUID;
    private static readonly Guid FragmentProviderInterfaceId = typeof(IRawElementProviderFragmentAbi).GUID;
    private static readonly Guid FragmentRootProviderInterfaceId = typeof(IRawElementProviderFragmentRootAbi).GUID;

    [DllImport("uiautomationcore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UiaClientsAreListening();

    [DllImport("uiautomationcore.dll")]
    private static extern IntPtr UiaReturnRawElementProvider(
        IntPtr hwnd,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr provider);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaHostProviderFromHwnd(
        IntPtr hwnd,
        out IntPtr provider);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaRaiseAutomationEvent(
        IntPtr provider,
        int eventId);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaRaiseAutomationPropertyChangedEvent(
        IntPtr provider,
        int propertyId,
        WindowsUiaVariant oldValue,
        WindowsUiaVariant newValue);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaRaiseStructureChangedEvent(
        IntPtr provider,
        StructureChangeType structureChangeType,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] int[] runtimeId,
        int runtimeIdLength);

    [DllImport("uiautomationcore.dll")]
    private static extern int UiaDisconnectProvider(
        IntPtr provider);

    [DllImport("oleaut32.dll")]
    private static extern IntPtr SafeArrayCreateVector(
        VarEnum variantType,
        int lowerBound,
        uint elementCount);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayAccessData(IntPtr safeArray, out IntPtr data);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayUnaccessData(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayPutElement(IntPtr safeArray, ref int index, IntPtr value);

    [DllImport("oleaut32.dll")]
    private static extern int SafeArrayDestroy(IntPtr safeArray);

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(ref WindowsUiaVariant value);

    public static bool ClientsAreListening
    {
        get
        {
            try
            {
                return UiaClientsAreListening();
            }
            catch (DllNotFoundException)
            {
                return false;
            }
        }
    }

    public static IntPtr ReturnRawElementProvider(
        IntPtr hwnd,
        IntPtr wParam,
        IntPtr lParam,
        WindowsUiaProvider provider)
    {
        IntPtr providerPointer = GetComInterfacePointer(provider, SimpleProviderInterfaceId);
        try
        {
            return UiaReturnRawElementProvider(hwnd, wParam, lParam, providerPointer);
        }
        finally
        {
            _ = Marshal.Release(providerPointer);
        }
    }

    public static IntPtr HostProviderFromHwnd(IntPtr hwnd)
    {
        Marshal.ThrowExceptionForHR(UiaHostProviderFromHwnd(hwnd, out IntPtr provider));
        return provider;
    }

    public static void RaiseAutomationEvent(WindowsUiaProvider provider, int eventId)
    {
        IntPtr providerPointer = GetComInterfacePointer(provider, SimpleProviderInterfaceId);
        try
        {
            Marshal.ThrowExceptionForHR(UiaRaiseAutomationEvent(providerPointer, eventId));
        }
        finally
        {
            _ = Marshal.Release(providerPointer);
        }
    }

    public static void RaiseAutomationPropertyChangedEvent(
        WindowsUiaProvider provider,
        int propertyId,
        object? oldValue,
        object? newValue)
    {
        IntPtr providerPointer = GetComInterfacePointer(provider, SimpleProviderInterfaceId);
        using WindowsUiaVariant oldVariant = WindowsUiaVariant.FromObject(oldValue);
        using WindowsUiaVariant newVariant = WindowsUiaVariant.FromObject(newValue);
        try
        {
            Marshal.ThrowExceptionForHR(
                UiaRaiseAutomationPropertyChangedEvent(
                    providerPointer,
                    propertyId,
                    oldVariant,
                    newVariant));
        }
        finally
        {
            _ = Marshal.Release(providerPointer);
        }
    }

    public static void RaiseStructureChangedEvent(
        WindowsUiaProvider provider,
        StructureChangeType changeType,
        int[] runtimeId)
    {
        IntPtr providerPointer = GetComInterfacePointer(provider, SimpleProviderInterfaceId);
        try
        {
            Marshal.ThrowExceptionForHR(
                UiaRaiseStructureChangedEvent(
                    providerPointer,
                    changeType,
                    runtimeId,
                    runtimeId.Length));
        }
        finally
        {
            _ = Marshal.Release(providerPointer);
        }
    }

    public static void DisconnectProvider(WindowsUiaProvider provider)
    {
        IntPtr providerPointer = GetComInterfacePointer(provider, SimpleProviderInterfaceId);
        try
        {
            _ = UiaDisconnectProvider(providerPointer);
        }
        catch (DllNotFoundException)
        {
            // UIAutomationCore is present on supported Windows desktops. Teardown remains safe on
            // deliberately stripped-down Windows test images.
        }
        finally
        {
            _ = Marshal.Release(providerPointer);
        }
    }

    public static IntPtr GetFragmentProviderPointer(WindowsUiaProvider provider)
        => GetComInterfacePointer(provider, FragmentProviderInterfaceId);

    public static IntPtr GetFragmentRootProviderPointer(WindowsUiaRootProvider provider)
        => GetComInterfacePointer(provider, FragmentRootProviderInterfaceId);

    public static IntPtr GetUnknownPointer(WindowsUiaProvider provider)
        => ComWrappers.GetOrCreateComInterfaceForObject(provider, CreateComInterfaceFlags.None);

    public static IntPtr CreateSafeArray(int[] values)
        => CreateBlittableSafeArray(VarEnum.VT_I4, values, static (source, destination) =>
            Marshal.Copy(source, 0, destination, source.Length));

    public static IntPtr CreateSafeArray(double[] values)
        => CreateBlittableSafeArray(VarEnum.VT_R8, values, static (source, destination) =>
            Marshal.Copy(source, 0, destination, source.Length));

    public static IntPtr CreateSafeArray(IReadOnlyList<WindowsUiaProvider> providers)
    {
        IntPtr safeArray = SafeArrayCreateVector(VarEnum.VT_UNKNOWN, 0, checked((uint)providers.Count));
        if (safeArray == IntPtr.Zero)
            throw new OutOfMemoryException("Unable to allocate a UI Automation provider array.");

        try
        {
            for (int index = 0; index < providers.Count; index++)
            {
                IntPtr providerPointer = GetUnknownPointer(providers[index]);
                try
                {
                    Marshal.ThrowExceptionForHR(SafeArrayPutElement(safeArray, ref index, providerPointer));
                }
                finally
                {
                    _ = Marshal.Release(providerPointer);
                }
            }

            return safeArray;
        }
        catch
        {
            _ = SafeArrayDestroy(safeArray);
            throw;
        }
    }

    public static int ClearVariant(ref WindowsUiaVariant value) => VariantClear(ref value);

    private static IntPtr GetComInterfacePointer(WindowsUiaProvider provider, Guid interfaceId)
    {
        IntPtr unknown = GetUnknownPointer(provider);
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in interfaceId, out IntPtr result));
            return result;
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    private static IntPtr CreateBlittableSafeArray<T>(
        VarEnum variantType,
        T[] values,
        Action<T[], IntPtr> copy)
    {
        IntPtr safeArray = SafeArrayCreateVector(variantType, 0, checked((uint)values.Length));
        if (safeArray == IntPtr.Zero)
            throw new OutOfMemoryException("Unable to allocate a UI Automation value array.");

        bool accessed = false;
        try
        {
            Marshal.ThrowExceptionForHR(SafeArrayAccessData(safeArray, out IntPtr data));
            accessed = true;
            copy(values, data);
            Marshal.ThrowExceptionForHR(SafeArrayUnaccessData(safeArray));
            accessed = false;
            return safeArray;
        }
        catch
        {
            if (accessed)
                _ = SafeArrayUnaccessData(safeArray);
            _ = SafeArrayDestroy(safeArray);
            throw;
        }
    }
}
