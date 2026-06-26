using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ModernFormsNext.WindowKit.Platform.Accessibility;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32
{
    /// <summary>
    /// Adapts a platform-neutral accessibility object to the Windows MSAA <c>IAccessible</c> contract.
    /// </summary>
    /// <remarks>
    /// The Windows backend exposes this object from <c>WM_GETOBJECT</c>. It deliberately consumes
    /// only <see cref="IPlatformAccessibleObject"/> so the backend does not reference
    /// ModernFormsNext controls or shared framework accessibility implementation types.
    /// </remarks>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    internal sealed class WindowsMsaaAccessibleObject : IWindowsMsaaAccessible
    {
        private const int ChildIdSelf = 0;
        private const int ErrorInvalidArgument = unchecked((int)0x80070057);

        private readonly WindowsMsaaAccessibleObjectCache cache;

        private WindowsMsaaAccessibleObject(IPlatformAccessibleObject platformObject, WindowsMsaaAccessibleObjectCache cache)
        {
            PlatformObject = platformObject;
            this.cache = cache;
        }

        /// <summary>
        /// Gets the <c>IAccessible</c> interface identifier used by <c>LresultFromObject</c>.
        /// </summary>
        public static Guid InterfaceId => new("618736E0-3C3D-11CF-810C-00AA00389B71");

        /// <summary>
        /// Gets the platform-neutral object represented by this MSAA provider.
        /// </summary>
        public IPlatformAccessibleObject PlatformObject { get; }

        /// <inheritdoc/>
        public object? accParent => ToVariant(PlatformObject.Parent);

        /// <inheritdoc/>
        public int accChildCount => PlatformObject.GetChildCount();

        /// <summary>
        /// Creates a root MSAA provider and its child provider cache.
        /// </summary>
        /// <param name="platformObject">The root platform-neutral accessibility object.</param>
        /// <returns>The MSAA provider that wraps <paramref name="platformObject"/>.</returns>
        public static WindowsMsaaAccessibleObject CreateRoot(IPlatformAccessibleObject platformObject)
        {
            var cache = new WindowsMsaaAccessibleObjectCache();
            return cache.GetOrCreate(platformObject);
        }

        /// <inheritdoc/>
        public object? get_accChild(object varChild)
        {
            var childId = GetChildId(varChild);

            if (childId == ChildIdSelf)
                return null;

            return ToVariant(GetChildById(childId));
        }

        /// <inheritdoc/>
        public string? get_accName(object varChild) => Resolve(varChild).Name;

        /// <inheritdoc/>
        public string? get_accValue(object varChild) => Resolve(varChild).Value;

        /// <inheritdoc/>
        public string? get_accDescription(object varChild) => Resolve(varChild).Description;

        /// <inheritdoc/>
        public object get_accRole(object varChild) => Resolve(varChild).Role;

        /// <inheritdoc/>
        public object get_accState(object varChild) => Resolve(varChild).State;

        /// <inheritdoc/>
        public string? get_accHelp(object varChild) => Resolve(varChild).Help;

        /// <inheritdoc/>
        public void get_accHelpTopic(out string? pszHelpFile, object varChild, out int pidTopic)
        {
            pidTopic = Resolve(varChild).GetHelpTopic(out pszHelpFile);
        }

        /// <inheritdoc/>
        public string? get_accKeyboardShortcut(object varChild) => Resolve(varChild).KeyboardShortcut;

        /// <inheritdoc/>
        public object? accFocus => ToVariant(PlatformObject.GetFocused());

        /// <inheritdoc/>
        public object? accSelection => ToVariant(PlatformObject.GetSelected());

        /// <inheritdoc/>
        public string? get_accDefaultAction(object varChild) => Resolve(varChild).DefaultAction;

        /// <inheritdoc/>
        public void accSelect(int flagsSelect, object varChild) => Resolve(varChild).Select(flagsSelect);

        /// <inheritdoc/>
        public void accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcxHeight, object varChild)
        {
            var bounds = Resolve(varChild).Bounds;
            pxLeft = (int)Math.Round(bounds.X);
            pyTop = (int)Math.Round(bounds.Y);
            pcxWidth = (int)Math.Round(bounds.Width);
            pcxHeight = (int)Math.Round(bounds.Height);
        }

        /// <inheritdoc/>
        public object? accNavigate(int navDir, object varStart)
        {
            if (ToNavigation(navDir) is not { } direction)
                return null;

            var start = Resolve(varStart);
            return ToVariant(start.Navigate(direction));
        }

        /// <inheritdoc/>
        public object? accHitTest(int xLeft, int yTop)
        {
            var hit = PlatformObject.HitTest(xLeft, yTop);
            return ReferenceEquals(hit, PlatformObject) ? ChildIdSelf : ToVariant(hit);
        }

        /// <inheritdoc/>
        public void accDoDefaultAction(object varChild) => Resolve(varChild).DoDefaultAction();

        /// <inheritdoc/>
        public void put_accName(object varChild, string? szName) => Resolve(varChild).Name = szName;

        /// <inheritdoc/>
        public void put_accValue(object varChild, string? szValue) => Resolve(varChild).Value = szValue;

        private static int GetChildId(object? varChild)
        {
            if (varChild is null)
                return ChildIdSelf;

            if (varChild is int childId)
                return childId;

            if (varChild is short shortChildId)
                return shortChildId;

            if (varChild is long longChildId && longChildId >= int.MinValue && longChildId <= int.MaxValue)
                return (int)longChildId;

            return ChildIdSelf;
        }

        private static PlatformAccessibleNavigation? ToNavigation(int navDir)
            => navDir switch
            {
                0x1 => PlatformAccessibleNavigation.Up,
                0x2 => PlatformAccessibleNavigation.Down,
                0x3 => PlatformAccessibleNavigation.Left,
                0x4 => PlatformAccessibleNavigation.Right,
                0x5 => PlatformAccessibleNavigation.Next,
                0x6 => PlatformAccessibleNavigation.Previous,
                0x7 => PlatformAccessibleNavigation.FirstChild,
                0x8 => PlatformAccessibleNavigation.LastChild,
                _ => null
            };

        private IPlatformAccessibleObject Resolve(object varChild)
        {
            var childId = GetChildId(varChild);
            return childId == ChildIdSelf ? PlatformObject : GetChildById(childId);
        }

        private IPlatformAccessibleObject GetChildById(int childId)
        {
            if (childId <= 0)
                ThrowInvalidChildId(childId);

            var child = PlatformObject.GetChild(childId - 1);
            return child ?? ThrowInvalidChildId(childId);
        }

        private object? ToVariant(IPlatformAccessibleObject? platformObject)
        {
            if (platformObject is null)
                return null;

            return ReferenceEquals(platformObject, PlatformObject)
                ? ChildIdSelf
                : cache.GetOrCreate(platformObject);
        }

        private static IPlatformAccessibleObject ThrowInvalidChildId(int childId)
            => throw new COMException($"Invalid MSAA child identifier {childId}.", ErrorInvalidArgument);

        private sealed class WindowsMsaaAccessibleObjectCache
        {
            private readonly ConditionalWeakTable<IPlatformAccessibleObject, WindowsMsaaAccessibleObject> objects = new();

            public WindowsMsaaAccessibleObject GetOrCreate(IPlatformAccessibleObject platformObject)
                => objects.GetValue(platformObject, value => new WindowsMsaaAccessibleObject(value, this));
        }
    }

    /// <summary>
    /// Defines the Windows MSAA <c>IAccessible</c> COM contract used by accessibility clients.
    /// </summary>
    [ComVisible(true)]
    [Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    internal interface IWindowsMsaaAccessible
    {
        /// <summary>
        /// Gets the parent accessible object.
        /// </summary>
        [DispId(-5000)]
        object? accParent
        {
            [return: MarshalAs(UnmanagedType.Struct)]
            get;
        }

        /// <summary>
        /// Gets the number of child accessible objects.
        /// </summary>
        [DispId(-5001)]
        int accChildCount { get; }

        /// <summary>
        /// Gets a child accessible object by MSAA child identifier.
        /// </summary>
        /// <param name="varChild">The MSAA child identifier.</param>
        /// <returns>The child accessible object, or <see langword="null"/> when the child is represented by the current object.</returns>
        [DispId(-5002)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object? get_accChild([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the accessible name.
        /// </summary>
        [DispId(-5003)]
        string? get_accName([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the accessible value.
        /// </summary>
        [DispId(-5004)]
        string? get_accValue([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the accessible description.
        /// </summary>
        [DispId(-5005)]
        string? get_accDescription([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the MSAA role.
        /// </summary>
        [DispId(-5006)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object get_accRole([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the MSAA state flags.
        /// </summary>
        [DispId(-5007)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object get_accState([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets help text.
        /// </summary>
        [DispId(-5008)]
        string? get_accHelp([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets help topic information.
        /// </summary>
        [DispId(-5009)]
        void get_accHelpTopic(
            [MarshalAs(UnmanagedType.BStr)] out string? pszHelpFile,
            [MarshalAs(UnmanagedType.Struct)] object varChild,
            out int pidTopic);

        /// <summary>
        /// Gets the keyboard shortcut text.
        /// </summary>
        [DispId(-5010)]
        string? get_accKeyboardShortcut([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets the focused accessible object.
        /// </summary>
        [DispId(-5011)]
        object? accFocus
        {
            [return: MarshalAs(UnmanagedType.Struct)]
            get;
        }

        /// <summary>
        /// Gets the selected accessible object.
        /// </summary>
        [DispId(-5012)]
        object? accSelection
        {
            [return: MarshalAs(UnmanagedType.Struct)]
            get;
        }

        /// <summary>
        /// Gets the default action text.
        /// </summary>
        [DispId(-5013)]
        string? get_accDefaultAction([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Selects or focuses the object.
        /// </summary>
        [DispId(-5014)]
        void accSelect(int flagsSelect, [MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Gets object bounds in screen coordinates.
        /// </summary>
        [DispId(-5015)]
        void accLocation(
            out int pxLeft,
            out int pyTop,
            out int pcxWidth,
            out int pcxHeight,
            [MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Navigates to a related object.
        /// </summary>
        [DispId(-5016)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object? accNavigate(int navDir, [MarshalAs(UnmanagedType.Struct)] object varStart);

        /// <summary>
        /// Gets the object at a screen coordinate.
        /// </summary>
        [DispId(-5017)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object? accHitTest(int xLeft, int yTop);

        /// <summary>
        /// Performs the default action.
        /// </summary>
        [DispId(-5018)]
        void accDoDefaultAction([MarshalAs(UnmanagedType.Struct)] object varChild);

        /// <summary>
        /// Sets the accessible name.
        /// </summary>
        [DispId(-5003)]
        void put_accName([MarshalAs(UnmanagedType.Struct)] object varChild, string? szName);

        /// <summary>
        /// Sets the accessible value.
        /// </summary>
        [DispId(-5004)]
        void put_accValue([MarshalAs(UnmanagedType.Struct)] object varChild, string? szValue);
    }
}
