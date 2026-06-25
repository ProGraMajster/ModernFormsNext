using ModernFormsNext.WindowKit.Backend.Windows.Win32Com.Impl;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32Com;

/// <summary>
/// Registers generated Win32 MicroCom metadata with the shared MicroCom runtime.
/// </summary>
/// <remarks>
/// The MicroCom generator emits one registration method for each generated proxy and vtable.
/// Those methods are not invoked automatically by the normal Windows backend build, so the
/// backend initializes them explicitly before shell dialogs, clipboard, or drag-and-drop code can
/// call QueryInterface. This type is intentionally Windows-specific and must remain in the
/// Windows backend assembly.
/// </remarks>
internal static unsafe class Win32ComRegistration
{
    private static bool initialized;
    private static readonly object sync = new();

    /// <summary>
    /// Initializes all generated Win32 MicroCom registrations once for the current process.
    /// </summary>
    /// <remarks>
    /// Calling this method more than once is harmless. The registration tables are process-wide
    /// static state inside <see cref="ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime"/>.
    /// </remarks>
    public static void Initialize()
    {
        if (initialized)
            return;

        lock (sync)
        {
            if (initialized)
                return;

            RegisterProxies();
            RegisterVTables();

            initialized = true;
        }
    }

    private static void RegisterProxies()
    {
        __MicroComIShellItemProxy.__MicroComModuleInit();
        __MicroComIShellItemArrayProxy.__MicroComModuleInit();
        __MicroComIModalWindowProxy.__MicroComModuleInit();
        __MicroComIFileDialogProxy.__MicroComModuleInit();
        __MicroComIFileOpenDialogProxy.__MicroComModuleInit();
        __MicroComIEnumFORMATETCProxy.__MicroComModuleInit();
        __MicroComIDataObjectProxy.__MicroComModuleInit();
        __MicroComIDropSourceProxy.__MicroComModuleInit();
        __MicroComIDropTargetProxy.__MicroComModuleInit();
    }

    private static void RegisterVTables()
    {
        __MicroComIShellItemVTable.__MicroComModuleInit();
        __MicroComIShellItemArrayVTable.__MicroComModuleInit();
        __MicroComIModalWindowVTable.__MicroComModuleInit();
        __MicroComIFileDialogVTable.__MicroComModuleInit();
        __MicroComIFileOpenDialogVTable.__MicroComModuleInit();
        __MicroComIEnumFORMATETCVTable.__MicroComModuleInit();
        __MicroComIDataObjectVTable.__MicroComModuleInit();
        __MicroComIDropSourceVTable.__MicroComModuleInit();
        __MicroComIDropTargetVTable.__MicroComModuleInit();
    }
}
