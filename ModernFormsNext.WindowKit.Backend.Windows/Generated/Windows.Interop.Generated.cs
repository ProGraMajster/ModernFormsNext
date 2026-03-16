#pragma warning disable 108
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using ModernFormsNext.WindowKit.Backend.MicroCom;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32Com
{
    [System.Flags()]
    internal enum FILEOPENDIALOGOPTIONS
    {
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_STRICTFILETYPES = 0x00000004,
        FOS_NOCHANGEDIR = 0x00000008,
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_ALLNONSTORAGEITEMS = 0x00000080,
        FOS_NOVALIDATE = 0x00000100,
        FOS_ALLOWMULTISELECT = 0x00000200,
        FOS_PATHMUSTEXIST = 0x00000800,
        FOS_FILEMUSTEXIST = 0x00001000,
        FOS_CREATEPROMPT = 0x00002000,
        FOS_SHAREAWARE = 0x00004000,
        FOS_NOREADONLYRETURN = 0x00008000,
        FOS_NOTESTFILECREATE = 0x00010000,
        FOS_HIDEMRUPLACES = 0x00020000,
        FOS_HIDEPINNEDPLACES = 0x00040000,
        FOS_NODEREFERENCELINKS = 0x00100000,
        FOS_DONTADDTORECENT = 0x02000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000
    }

    [System.Flags()]
    internal enum DropEffect
    {
        None = 0,
        Copy = 1,
        Move = 2,
        Link = 4,
        Scroll = -2147483648
    }

    internal unsafe partial interface IShellItem : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        void* BindToHandler(void* pbc, System.Guid* bhid, System.Guid* riid);
        IShellItem Parent { get; }

        System.Char* GetDisplayName(uint sigdnName);
        uint GetAttributes(uint sfgaoMask);
        int Compare(IShellItem psi, uint hint);
    }

    internal unsafe partial interface IShellItemArray : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        void* BindToHandler(void* pbc, System.Guid* bhid, System.Guid* riid);
        void* GetPropertyStore(ushort flags, System.Guid* riid);
        void* GetPropertyDescriptionList(void* keyType, System.Guid* riid);
        ushort GetAttributes(int AttribFlags, ushort sfgaoMask);
        int Count { get; }

        IShellItem GetItemAt(int dwIndex);
        void* EnumItems();
    }

    internal unsafe partial interface IModalWindow : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        int Show(IntPtr hwndOwner);
    }

    internal unsafe partial interface IFileDialog : IModalWindow
    {
        void SetFileTypes(ushort cFileTypes, void* rgFilterSpec);
        void SetFileTypeIndex(ushort iFileType);
        ushort FileTypeIndex { get; }

        int Advise(void* pfde);
        void Unadvise(int dwCookie);
        void SetOptions(FILEOPENDIALOGOPTIONS fos);
        FILEOPENDIALOGOPTIONS Options { get; }

        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        IShellItem Folder { get; }

        IShellItem CurrentSelection { get; }

        void SetFileName(System.Char* pszName);
        System.Char* FileName { get; }

        void SetTitle(System.Char* pszTitle);
        void SetOkButtonLabel(System.Char* pszText);
        void SetFileNameLabel(System.Char* pszLabel);
        IShellItem Result { get; }

        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension(System.Char* pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(System.Guid* guid);
        void ClearClientData();
        void SetFilter(void* pFilter);
    }

    internal unsafe partial interface IFileOpenDialog : IFileDialog
    {
        IShellItemArray Results { get; }

        IShellItemArray SelectedItems { get; }
    }

    internal unsafe partial interface IEnumFORMATETC : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        uint Next(uint celt, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* rgelt, uint* pceltFetched);
        uint Skip(uint celt);
        void Reset();
        IEnumFORMATETC Clone();
    }

    internal unsafe partial interface IDataObject : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        uint GetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium);
        uint GetDataHere(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium);
        uint QueryGetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc);
        ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC GetCanonicalFormatEtc(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatectIn);
        uint SetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium, int fRelease);
        IEnumFORMATETC EnumFormatEtc(int dwDirection);
        int DAdvise(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, int advf, void* pAdvSink);
        void DUnadvise(int dwConnection);
        void* EnumDAdvise();
    }

    internal unsafe partial interface IDropSource : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        int QueryContinueDrag(int fEscapePressed, int grfKeyState);
        int GiveFeedback(DropEffect dwEffect);
    }

    internal unsafe partial interface IDropTarget : ModernFormsNext.WindowKit.Backend.MicroCom.IUnknown
    {
        void DragEnter(IDataObject pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
        void DragOver(int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
        void DragLeave();
        void Drop(IDataObject pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
    }
}

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32Com.Impl
{
    unsafe internal partial class __MicroComIShellItemProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IShellItem
    {
        public void* BindToHandler(void* pbc, System.Guid* bhid, System.Guid* riid)
        {
            int __result;
            void* ppv = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pbc, bhid, riid, &ppv, (*PPV)[base.VTableSize + 0]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("BindToHandler failed", __result);
            return ppv;
        }

        public IShellItem Parent
        {
            get
            {
                int __result;
                void* __marshal_ppsi = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppsi, (*PPV)[base.VTableSize + 1]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetParent failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(__marshal_ppsi, true);
            }
        }

        public System.Char* GetDisplayName(uint sigdnName)
        {
            int __result;
            System.Char* ppszName = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, sigdnName, &ppszName, (*PPV)[base.VTableSize + 2]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetDisplayName failed", __result);
            return ppszName;
        }

        public uint GetAttributes(uint sfgaoMask)
        {
            int __result;
            uint psfgaoAttribs = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, sfgaoMask, &psfgaoAttribs, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetAttributes failed", __result);
            return psfgaoAttribs;
        }

        public int Compare(IShellItem psi, uint hint)
        {
            int __result;
            int piOrder = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(psi), hint, &piOrder, (*PPV)[base.VTableSize + 4]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Compare failed", __result);
            return piOrder;
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IShellItem), new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), (p, owns) => new __MicroComIShellItemProxy(p, owns));
        }

        public __MicroComIShellItemProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 5;
    }

    unsafe class __MicroComIShellItemVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int BindToHandlerDelegate(IntPtr @this, void* pbc, System.Guid* bhid, System.Guid* riid, void** ppv);
        static int BindToHandler(IntPtr @this, void* pbc, System.Guid* bhid, System.Guid* riid, void** ppv)
        {
            IShellItem __target = null;
            try
            {
                {
                    __target = (IShellItem)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.BindToHandler(pbc, bhid, riid);
                        *ppv = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetParentDelegate(IntPtr @this, void** ppsi);
        static int GetParent(IntPtr @this, void** ppsi)
        {
            IShellItem __target = null;
            try
            {
                {
                    __target = (IShellItem)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Parent;
                        *ppsi = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetDisplayNameDelegate(IntPtr @this, uint sigdnName, System.Char** ppszName);
        static int GetDisplayName(IntPtr @this, uint sigdnName, System.Char** ppszName)
        {
            IShellItem __target = null;
            try
            {
                {
                    __target = (IShellItem)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetDisplayName(sigdnName);
                        *ppszName = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetAttributesDelegate(IntPtr @this, uint sfgaoMask, uint* psfgaoAttribs);
        static int GetAttributes(IntPtr @this, uint sfgaoMask, uint* psfgaoAttribs)
        {
            IShellItem __target = null;
            try
            {
                {
                    __target = (IShellItem)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetAttributes(sfgaoMask);
                        *psfgaoAttribs = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int CompareDelegate(IntPtr @this, void* psi, uint hint, int* piOrder);
        static int Compare(IntPtr @this, void* psi, uint hint, int* piOrder)
        {
            IShellItem __target = null;
            try
            {
                {
                    __target = (IShellItem)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Compare(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(psi, false), hint);
                        *piOrder = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIShellItemVTable()
        {
            base.AddMethod((BindToHandlerDelegate)BindToHandler);
            base.AddMethod((GetParentDelegate)GetParent);
            base.AddMethod((GetDisplayNameDelegate)GetDisplayName);
            base.AddMethod((GetAttributesDelegate)GetAttributes);
            base.AddMethod((CompareDelegate)Compare);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IShellItem), new __MicroComIShellItemVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIShellItemArrayProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IShellItemArray
    {
        public void* BindToHandler(void* pbc, System.Guid* bhid, System.Guid* riid)
        {
            int __result;
            void* ppvOut = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pbc, bhid, riid, &ppvOut, (*PPV)[base.VTableSize + 0]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("BindToHandler failed", __result);
            return ppvOut;
        }

        public void* GetPropertyStore(ushort flags, System.Guid* riid)
        {
            int __result;
            void* ppv = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, flags, riid, &ppv, (*PPV)[base.VTableSize + 1]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetPropertyStore failed", __result);
            return ppv;
        }

        public void* GetPropertyDescriptionList(void* keyType, System.Guid* riid)
        {
            int __result;
            void* ppv = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, keyType, riid, &ppv, (*PPV)[base.VTableSize + 2]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetPropertyDescriptionList failed", __result);
            return ppv;
        }

        public ushort GetAttributes(int AttribFlags, ushort sfgaoMask)
        {
            int __result;
            ushort psfgaoAttribs = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, AttribFlags, sfgaoMask, &psfgaoAttribs, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetAttributes failed", __result);
            return psfgaoAttribs;
        }

        public int Count
        {
            get
            {
                int __result;
                int pdwNumItems = default;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &pdwNumItems, (*PPV)[base.VTableSize + 4]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetCount failed", __result);
                return pdwNumItems;
            }
        }

        public IShellItem GetItemAt(int dwIndex)
        {
            int __result;
            void* __marshal_ppsi = null;
            __result = (int)LocalInterop.CalliStdCallint(PPV, dwIndex, &__marshal_ppsi, (*PPV)[base.VTableSize + 5]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetItemAt failed", __result);
            return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(__marshal_ppsi, true);
        }

        public void* EnumItems()
        {
            int __result;
            void* ppenumShellItems = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, &ppenumShellItems, (*PPV)[base.VTableSize + 6]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("EnumItems failed", __result);
            return ppenumShellItems;
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IShellItemArray), new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), (p, owns) => new __MicroComIShellItemArrayProxy(p, owns));
        }

        public __MicroComIShellItemArrayProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 7;
    }

    unsafe class __MicroComIShellItemArrayVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int BindToHandlerDelegate(IntPtr @this, void* pbc, System.Guid* bhid, System.Guid* riid, void** ppvOut);
        static int BindToHandler(IntPtr @this, void* pbc, System.Guid* bhid, System.Guid* riid, void** ppvOut)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.BindToHandler(pbc, bhid, riid);
                        *ppvOut = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetPropertyStoreDelegate(IntPtr @this, ushort flags, System.Guid* riid, void** ppv);
        static int GetPropertyStore(IntPtr @this, ushort flags, System.Guid* riid, void** ppv)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetPropertyStore(flags, riid);
                        *ppv = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetPropertyDescriptionListDelegate(IntPtr @this, void* keyType, System.Guid* riid, void** ppv);
        static int GetPropertyDescriptionList(IntPtr @this, void* keyType, System.Guid* riid, void** ppv)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetPropertyDescriptionList(keyType, riid);
                        *ppv = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetAttributesDelegate(IntPtr @this, int AttribFlags, ushort sfgaoMask, ushort* psfgaoAttribs);
        static int GetAttributes(IntPtr @this, int AttribFlags, ushort sfgaoMask, ushort* psfgaoAttribs)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetAttributes(AttribFlags, sfgaoMask);
                        *psfgaoAttribs = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetCountDelegate(IntPtr @this, int* pdwNumItems);
        static int GetCount(IntPtr @this, int* pdwNumItems)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Count;
                        *pdwNumItems = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetItemAtDelegate(IntPtr @this, int dwIndex, void** ppsi);
        static int GetItemAt(IntPtr @this, int dwIndex, void** ppsi)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetItemAt(dwIndex);
                        *ppsi = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int EnumItemsDelegate(IntPtr @this, void** ppenumShellItems);
        static int EnumItems(IntPtr @this, void** ppenumShellItems)
        {
            IShellItemArray __target = null;
            try
            {
                {
                    __target = (IShellItemArray)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.EnumItems();
                        *ppenumShellItems = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIShellItemArrayVTable()
        {
            base.AddMethod((BindToHandlerDelegate)BindToHandler);
            base.AddMethod((GetPropertyStoreDelegate)GetPropertyStore);
            base.AddMethod((GetPropertyDescriptionListDelegate)GetPropertyDescriptionList);
            base.AddMethod((GetAttributesDelegate)GetAttributes);
            base.AddMethod((GetCountDelegate)GetCount);
            base.AddMethod((GetItemAtDelegate)GetItemAt);
            base.AddMethod((EnumItemsDelegate)EnumItems);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IShellItemArray), new __MicroComIShellItemArrayVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIModalWindowProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IModalWindow
    {
        public int Show(IntPtr hwndOwner)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, hwndOwner, (*PPV)[base.VTableSize + 0]);
            return __result;
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IModalWindow), new Guid("B4DB1657-70D7-485E-8E3E-6FCB5A5C1802"), (p, owns) => new __MicroComIModalWindowProxy(p, owns));
        }

        public __MicroComIModalWindowProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 1;
    }

    unsafe class __MicroComIModalWindowVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int ShowDelegate(IntPtr @this, IntPtr hwndOwner);
        static int Show(IntPtr @this, IntPtr hwndOwner)
        {
            IModalWindow __target = null;
            try
            {
                {
                    __target = (IModalWindow)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Show(hwndOwner);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        public __MicroComIModalWindowVTable()
        {
            base.AddMethod((ShowDelegate)Show);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IModalWindow), new __MicroComIModalWindowVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIFileDialogProxy : __MicroComIModalWindowProxy, IFileDialog
    {
        public void SetFileTypes(ushort cFileTypes, void* rgFilterSpec)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, cFileTypes, rgFilterSpec, (*PPV)[base.VTableSize + 0]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFileTypes failed", __result);
        }

        public void SetFileTypeIndex(ushort iFileType)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, iFileType, (*PPV)[base.VTableSize + 1]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFileTypeIndex failed", __result);
        }

        public ushort FileTypeIndex
        {
            get
            {
                int __result;
                ushort piFileType = default;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &piFileType, (*PPV)[base.VTableSize + 2]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetFileTypeIndex failed", __result);
                return piFileType;
            }
        }

        public int Advise(void* pfde)
        {
            int __result;
            int pdwCookie = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pfde, &pdwCookie, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Advise failed", __result);
            return pdwCookie;
        }

        public void Unadvise(int dwCookie)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, dwCookie, (*PPV)[base.VTableSize + 4]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Unadvise failed", __result);
        }

        public void SetOptions(FILEOPENDIALOGOPTIONS fos)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, fos, (*PPV)[base.VTableSize + 5]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetOptions failed", __result);
        }

        public FILEOPENDIALOGOPTIONS Options
        {
            get
            {
                int __result;
                FILEOPENDIALOGOPTIONS pfos = default;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &pfos, (*PPV)[base.VTableSize + 6]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetOptions failed", __result);
                return pfos;
            }
        }

        public void SetDefaultFolder(IShellItem psi)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(psi), (*PPV)[base.VTableSize + 7]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetDefaultFolder failed", __result);
        }

        public void SetFolder(IShellItem psi)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(psi), (*PPV)[base.VTableSize + 8]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFolder failed", __result);
        }

        public IShellItem Folder
        {
            get
            {
                int __result;
                void* __marshal_ppsi = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppsi, (*PPV)[base.VTableSize + 9]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetFolder failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(__marshal_ppsi, true);
            }
        }

        public IShellItem CurrentSelection
        {
            get
            {
                int __result;
                void* __marshal_ppsi = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppsi, (*PPV)[base.VTableSize + 10]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetCurrentSelection failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(__marshal_ppsi, true);
            }
        }

        public void SetFileName(System.Char* pszName)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pszName, (*PPV)[base.VTableSize + 11]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFileName failed", __result);
        }

        public System.Char* FileName
        {
            get
            {
                int __result;
                System.Char* pszName = default;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &pszName, (*PPV)[base.VTableSize + 12]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetFileName failed", __result);
                return pszName;
            }
        }

        public void SetTitle(System.Char* pszTitle)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pszTitle, (*PPV)[base.VTableSize + 13]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetTitle failed", __result);
        }

        public void SetOkButtonLabel(System.Char* pszText)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pszText, (*PPV)[base.VTableSize + 14]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetOkButtonLabel failed", __result);
        }

        public void SetFileNameLabel(System.Char* pszLabel)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pszLabel, (*PPV)[base.VTableSize + 15]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFileNameLabel failed", __result);
        }

        public IShellItem Result
        {
            get
            {
                int __result;
                void* __marshal_ppsi = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppsi, (*PPV)[base.VTableSize + 16]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetResult failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(__marshal_ppsi, true);
            }
        }

        public void AddPlace(IShellItem psi, int fdap)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(psi), fdap, (*PPV)[base.VTableSize + 17]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("AddPlace failed", __result);
        }

        public void SetDefaultExtension(System.Char* pszDefaultExtension)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pszDefaultExtension, (*PPV)[base.VTableSize + 18]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetDefaultExtension failed", __result);
        }

        public void Close(int hr)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, hr, (*PPV)[base.VTableSize + 19]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Close failed", __result);
        }

        public void SetClientGuid(System.Guid* guid)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, guid, (*PPV)[base.VTableSize + 20]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetClientGuid failed", __result);
        }

        public void ClearClientData()
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, (*PPV)[base.VTableSize + 21]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("ClearClientData failed", __result);
        }

        public void SetFilter(void* pFilter)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pFilter, (*PPV)[base.VTableSize + 22]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("SetFilter failed", __result);
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IFileDialog), new Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), (p, owns) => new __MicroComIFileDialogProxy(p, owns));
        }

        public __MicroComIFileDialogProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 23;
    }

    unsafe class __MicroComIFileDialogVTable : __MicroComIModalWindowVTable
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFileTypesDelegate(IntPtr @this, ushort cFileTypes, void* rgFilterSpec);
        static int SetFileTypes(IntPtr @this, ushort cFileTypes, void* rgFilterSpec)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFileTypes(cFileTypes, rgFilterSpec);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFileTypeIndexDelegate(IntPtr @this, ushort iFileType);
        static int SetFileTypeIndex(IntPtr @this, ushort iFileType)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFileTypeIndex(iFileType);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetFileTypeIndexDelegate(IntPtr @this, ushort* piFileType);
        static int GetFileTypeIndex(IntPtr @this, ushort* piFileType)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.FileTypeIndex;
                        *piFileType = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int AdviseDelegate(IntPtr @this, void* pfde, int* pdwCookie);
        static int Advise(IntPtr @this, void* pfde, int* pdwCookie)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Advise(pfde);
                        *pdwCookie = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int UnadviseDelegate(IntPtr @this, int dwCookie);
        static int Unadvise(IntPtr @this, int dwCookie)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.Unadvise(dwCookie);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetOptionsDelegate(IntPtr @this, FILEOPENDIALOGOPTIONS fos);
        static int SetOptions(IntPtr @this, FILEOPENDIALOGOPTIONS fos)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetOptions(fos);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetOptionsDelegate(IntPtr @this, FILEOPENDIALOGOPTIONS* pfos);
        static int GetOptions(IntPtr @this, FILEOPENDIALOGOPTIONS* pfos)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Options;
                        *pfos = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetDefaultFolderDelegate(IntPtr @this, void* psi);
        static int SetDefaultFolder(IntPtr @this, void* psi)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetDefaultFolder(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(psi, false));
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFolderDelegate(IntPtr @this, void* psi);
        static int SetFolder(IntPtr @this, void* psi)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFolder(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(psi, false));
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetFolderDelegate(IntPtr @this, void** ppsi);
        static int GetFolder(IntPtr @this, void** ppsi)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Folder;
                        *ppsi = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetCurrentSelectionDelegate(IntPtr @this, void** ppsi);
        static int GetCurrentSelection(IntPtr @this, void** ppsi)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.CurrentSelection;
                        *ppsi = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFileNameDelegate(IntPtr @this, System.Char* pszName);
        static int SetFileName(IntPtr @this, System.Char* pszName)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFileName(pszName);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetFileNameDelegate(IntPtr @this, System.Char** pszName);
        static int GetFileName(IntPtr @this, System.Char** pszName)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.FileName;
                        *pszName = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetTitleDelegate(IntPtr @this, System.Char* pszTitle);
        static int SetTitle(IntPtr @this, System.Char* pszTitle)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetTitle(pszTitle);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetOkButtonLabelDelegate(IntPtr @this, System.Char* pszText);
        static int SetOkButtonLabel(IntPtr @this, System.Char* pszText)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetOkButtonLabel(pszText);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFileNameLabelDelegate(IntPtr @this, System.Char* pszLabel);
        static int SetFileNameLabel(IntPtr @this, System.Char* pszLabel)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFileNameLabel(pszLabel);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetResultDelegate(IntPtr @this, void** ppsi);
        static int GetResult(IntPtr @this, void** ppsi)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Result;
                        *ppsi = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int AddPlaceDelegate(IntPtr @this, void* psi, int fdap);
        static int AddPlace(IntPtr @this, void* psi, int fdap)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.AddPlace(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItem>(psi, false), fdap);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetDefaultExtensionDelegate(IntPtr @this, System.Char* pszDefaultExtension);
        static int SetDefaultExtension(IntPtr @this, System.Char* pszDefaultExtension)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetDefaultExtension(pszDefaultExtension);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int CloseDelegate(IntPtr @this, int hr);
        static int Close(IntPtr @this, int hr)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.Close(hr);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetClientGuidDelegate(IntPtr @this, System.Guid* guid);
        static int SetClientGuid(IntPtr @this, System.Guid* guid)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetClientGuid(guid);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int ClearClientDataDelegate(IntPtr @this);
        static int ClearClientData(IntPtr @this)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.ClearClientData();
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int SetFilterDelegate(IntPtr @this, void* pFilter);
        static int SetFilter(IntPtr @this, void* pFilter)
        {
            IFileDialog __target = null;
            try
            {
                {
                    __target = (IFileDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.SetFilter(pFilter);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIFileDialogVTable()
        {
            base.AddMethod((SetFileTypesDelegate)SetFileTypes);
            base.AddMethod((SetFileTypeIndexDelegate)SetFileTypeIndex);
            base.AddMethod((GetFileTypeIndexDelegate)GetFileTypeIndex);
            base.AddMethod((AdviseDelegate)Advise);
            base.AddMethod((UnadviseDelegate)Unadvise);
            base.AddMethod((SetOptionsDelegate)SetOptions);
            base.AddMethod((GetOptionsDelegate)GetOptions);
            base.AddMethod((SetDefaultFolderDelegate)SetDefaultFolder);
            base.AddMethod((SetFolderDelegate)SetFolder);
            base.AddMethod((GetFolderDelegate)GetFolder);
            base.AddMethod((GetCurrentSelectionDelegate)GetCurrentSelection);
            base.AddMethod((SetFileNameDelegate)SetFileName);
            base.AddMethod((GetFileNameDelegate)GetFileName);
            base.AddMethod((SetTitleDelegate)SetTitle);
            base.AddMethod((SetOkButtonLabelDelegate)SetOkButtonLabel);
            base.AddMethod((SetFileNameLabelDelegate)SetFileNameLabel);
            base.AddMethod((GetResultDelegate)GetResult);
            base.AddMethod((AddPlaceDelegate)AddPlace);
            base.AddMethod((SetDefaultExtensionDelegate)SetDefaultExtension);
            base.AddMethod((CloseDelegate)Close);
            base.AddMethod((SetClientGuidDelegate)SetClientGuid);
            base.AddMethod((ClearClientDataDelegate)ClearClientData);
            base.AddMethod((SetFilterDelegate)SetFilter);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IFileDialog), new __MicroComIFileDialogVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIFileOpenDialogProxy : __MicroComIFileDialogProxy, IFileOpenDialog
    {
        public IShellItemArray Results
        {
            get
            {
                int __result;
                void* __marshal_ppenum = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppenum, (*PPV)[base.VTableSize + 0]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetResults failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItemArray>(__marshal_ppenum, true);
            }
        }

        public IShellItemArray SelectedItems
        {
            get
            {
                int __result;
                void* __marshal_ppsai = null;
                __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppsai, (*PPV)[base.VTableSize + 1]);
                if (__result != 0)
                    throw new System.Runtime.InteropServices.COMException("GetSelectedItems failed", __result);
                return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IShellItemArray>(__marshal_ppsai, true);
            }
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IFileOpenDialog), new Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), (p, owns) => new __MicroComIFileOpenDialogProxy(p, owns));
        }

        public __MicroComIFileOpenDialogProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 2;
    }

    unsafe class __MicroComIFileOpenDialogVTable : __MicroComIFileDialogVTable
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetResultsDelegate(IntPtr @this, void** ppenum);
        static int GetResults(IntPtr @this, void** ppenum)
        {
            IFileOpenDialog __target = null;
            try
            {
                {
                    __target = (IFileOpenDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Results;
                        *ppenum = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetSelectedItemsDelegate(IntPtr @this, void** ppsai);
        static int GetSelectedItems(IntPtr @this, void** ppsai)
        {
            IFileOpenDialog __target = null;
            try
            {
                {
                    __target = (IFileOpenDialog)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.SelectedItems;
                        *ppsai = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIFileOpenDialogVTable()
        {
            base.AddMethod((GetResultsDelegate)GetResults);
            base.AddMethod((GetSelectedItemsDelegate)GetSelectedItems);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IFileOpenDialog), new __MicroComIFileOpenDialogVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIEnumFORMATETCProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IEnumFORMATETC
    {
        public uint Next(uint celt, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* rgelt, uint* pceltFetched)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, celt, rgelt, pceltFetched, (*PPV)[base.VTableSize + 0]);
            return __result;
        }

        public uint Skip(uint celt)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, celt, (*PPV)[base.VTableSize + 1]);
            return __result;
        }

        public void Reset()
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, (*PPV)[base.VTableSize + 2]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Reset failed", __result);
        }

        public IEnumFORMATETC Clone()
        {
            int __result;
            void* __marshal_ppenum = null;
            __result = (int)LocalInterop.CalliStdCallint(PPV, &__marshal_ppenum, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Clone failed", __result);
            return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IEnumFORMATETC>(__marshal_ppenum, true);
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IEnumFORMATETC), new Guid("00000103-0000-0000-C000-000000000046"), (p, owns) => new __MicroComIEnumFORMATETCProxy(p, owns));
        }

        public __MicroComIEnumFORMATETCProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 4;
    }

    unsafe class __MicroComIEnumFORMATETCVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint NextDelegate(IntPtr @this, uint celt, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* rgelt, uint* pceltFetched);
        static uint Next(IntPtr @this, uint celt, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* rgelt, uint* pceltFetched)
        {
            IEnumFORMATETC __target = null;
            try
            {
                {
                    __target = (IEnumFORMATETC)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Next(celt, rgelt, pceltFetched);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint SkipDelegate(IntPtr @this, uint celt);
        static uint Skip(IntPtr @this, uint celt)
        {
            IEnumFORMATETC __target = null;
            try
            {
                {
                    __target = (IEnumFORMATETC)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Skip(celt);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int ResetDelegate(IntPtr @this);
        static int Reset(IntPtr @this)
        {
            IEnumFORMATETC __target = null;
            try
            {
                {
                    __target = (IEnumFORMATETC)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.Reset();
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int CloneDelegate(IntPtr @this, void** ppenum);
        static int Clone(IntPtr @this, void** ppenum)
        {
            IEnumFORMATETC __target = null;
            try
            {
                {
                    __target = (IEnumFORMATETC)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.Clone();
                        *ppenum = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIEnumFORMATETCVTable()
        {
            base.AddMethod((NextDelegate)Next);
            base.AddMethod((SkipDelegate)Skip);
            base.AddMethod((ResetDelegate)Reset);
            base.AddMethod((CloneDelegate)Clone);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IEnumFORMATETC), new __MicroComIEnumFORMATETCVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIDataObjectProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IDataObject
    {
        public uint GetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, pformatetcIn, pmedium, (*PPV)[base.VTableSize + 0]);
            return __result;
        }

        public uint GetDataHere(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, pformatetc, pmedium, (*PPV)[base.VTableSize + 1]);
            return __result;
        }

        public uint QueryGetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, pformatetc, (*PPV)[base.VTableSize + 2]);
            return __result;
        }

        public ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC GetCanonicalFormatEtc(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatectIn)
        {
            int __result;
            ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC pformatetcOut = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pformatectIn, &pformatetcOut, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("GetCanonicalFormatEtc failed", __result);
            return pformatetcOut;
        }

        public uint SetData(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium, int fRelease)
        {
            uint __result;
            __result = (uint)LocalInterop.CalliStdCalluint(PPV, pformatetc, pmedium, fRelease, (*PPV)[base.VTableSize + 4]);
            return __result;
        }

        public IEnumFORMATETC EnumFormatEtc(int dwDirection)
        {
            int __result;
            void* __marshal_ppenumFormatEtc = null;
            __result = (int)LocalInterop.CalliStdCallint(PPV, dwDirection, &__marshal_ppenumFormatEtc, (*PPV)[base.VTableSize + 5]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("EnumFormatEtc failed", __result);
            return ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IEnumFORMATETC>(__marshal_ppenumFormatEtc, true);
        }

        public int DAdvise(ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, int advf, void* pAdvSink)
        {
            int __result;
            int pdwConnection = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, pformatetc, advf, pAdvSink, &pdwConnection, (*PPV)[base.VTableSize + 6]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("DAdvise failed", __result);
            return pdwConnection;
        }

        public void DUnadvise(int dwConnection)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, dwConnection, (*PPV)[base.VTableSize + 7]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("DUnadvise failed", __result);
        }

        public void* EnumDAdvise()
        {
            int __result;
            void* ppenumAdvise = default;
            __result = (int)LocalInterop.CalliStdCallint(PPV, &ppenumAdvise, (*PPV)[base.VTableSize + 8]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("EnumDAdvise failed", __result);
            return ppenumAdvise;
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IDataObject), new Guid("0000010e-0000-0000-C000-000000000046"), (p, owns) => new __MicroComIDataObjectProxy(p, owns));
        }

        public __MicroComIDataObjectProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 9;
    }

    unsafe class __MicroComIDataObjectVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint GetDataDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium);
        static uint GetData(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetData(pformatetcIn, pmedium);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint GetDataHereDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium);
        static uint GetDataHere(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetDataHere(pformatetc, pmedium);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint QueryGetDataDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc);
        static uint QueryGetData(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.QueryGetData(pformatetc);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GetCanonicalFormatEtcDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatectIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcOut);
        static int GetCanonicalFormatEtc(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatectIn, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetcOut)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GetCanonicalFormatEtc(pformatectIn);
                        *pformatetcOut = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate uint SetDataDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium, int fRelease);
        static uint SetData(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.STGMEDIUM* pmedium, int fRelease)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.SetData(pformatetc, pmedium, fRelease);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int EnumFormatEtcDelegate(IntPtr @this, int dwDirection, void** ppenumFormatEtc);
        static int EnumFormatEtc(IntPtr @this, int dwDirection, void** ppenumFormatEtc)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.EnumFormatEtc(dwDirection);
                        *ppenumFormatEtc = ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(__result, true);
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DAdviseDelegate(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, int advf, void* pAdvSink, int* pdwConnection);
        static int DAdvise(IntPtr @this, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.FORMATETC* pformatetc, int advf, void* pAdvSink, int* pdwConnection)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.DAdvise(pformatetc, advf, pAdvSink);
                        *pdwConnection = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DUnadviseDelegate(IntPtr @this, int dwConnection);
        static int DUnadvise(IntPtr @this, int dwConnection)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.DUnadvise(dwConnection);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int EnumDAdviseDelegate(IntPtr @this, void** ppenumAdvise);
        static int EnumDAdvise(IntPtr @this, void** ppenumAdvise)
        {
            IDataObject __target = null;
            try
            {
                {
                    __target = (IDataObject)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.EnumDAdvise();
                        *ppenumAdvise = __result;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIDataObjectVTable()
        {
            base.AddMethod((GetDataDelegate)GetData);
            base.AddMethod((GetDataHereDelegate)GetDataHere);
            base.AddMethod((QueryGetDataDelegate)QueryGetData);
            base.AddMethod((GetCanonicalFormatEtcDelegate)GetCanonicalFormatEtc);
            base.AddMethod((SetDataDelegate)SetData);
            base.AddMethod((EnumFormatEtcDelegate)EnumFormatEtc);
            base.AddMethod((DAdviseDelegate)DAdvise);
            base.AddMethod((DUnadviseDelegate)DUnadvise);
            base.AddMethod((EnumDAdviseDelegate)EnumDAdvise);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IDataObject), new __MicroComIDataObjectVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIDropSourceProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IDropSource
    {
        public int QueryContinueDrag(int fEscapePressed, int grfKeyState)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, fEscapePressed, grfKeyState, (*PPV)[base.VTableSize + 0]);
            return __result;
        }

        public int GiveFeedback(DropEffect dwEffect)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, dwEffect, (*PPV)[base.VTableSize + 1]);
            return __result;
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IDropSource), new Guid("00000121-0000-0000-C000-000000000046"), (p, owns) => new __MicroComIDropSourceProxy(p, owns));
        }

        public __MicroComIDropSourceProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 2;
    }

    unsafe class __MicroComIDropSourceVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int QueryContinueDragDelegate(IntPtr @this, int fEscapePressed, int grfKeyState);
        static int QueryContinueDrag(IntPtr @this, int fEscapePressed, int grfKeyState)
        {
            IDropSource __target = null;
            try
            {
                {
                    __target = (IDropSource)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.QueryContinueDrag(fEscapePressed, grfKeyState);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int GiveFeedbackDelegate(IntPtr @this, DropEffect dwEffect);
        static int GiveFeedback(IntPtr @this, DropEffect dwEffect)
        {
            IDropSource __target = null;
            try
            {
                {
                    __target = (IDropSource)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    {
                        var __result = __target.GiveFeedback(dwEffect);
                        return __result;
                    }
                }
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return default;
            }
        }

        public __MicroComIDropSourceVTable()
        {
            base.AddMethod((QueryContinueDragDelegate)QueryContinueDrag);
            base.AddMethod((GiveFeedbackDelegate)GiveFeedback);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IDropSource), new __MicroComIDropSourceVTable().CreateVTable());
    }

    unsafe internal partial class __MicroComIDropTargetProxy : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComProxyBase, IDropTarget
    {
        public void DragEnter(IDataObject pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(pDataObj), grfKeyState, pt, pdwEffect, (*PPV)[base.VTableSize + 0]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("DragEnter failed", __result);
        }

        public void DragOver(int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, grfKeyState, pt, pdwEffect, (*PPV)[base.VTableSize + 1]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("DragOver failed", __result);
        }

        public void DragLeave()
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, (*PPV)[base.VTableSize + 2]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("DragLeave failed", __result);
        }

        public void Drop(IDataObject pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            int __result;
            __result = (int)LocalInterop.CalliStdCallint(PPV, ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetNativePointer(pDataObj), grfKeyState, pt, pdwEffect, (*PPV)[base.VTableSize + 3]);
            if (__result != 0)
                throw new System.Runtime.InteropServices.COMException("Drop failed", __result);
        }

        static internal void __MicroComModuleInit()
        {
            ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.Register(typeof(IDropTarget), new Guid("00000122-0000-0000-C000-000000000046"), (p, owns) => new __MicroComIDropTargetProxy(p, owns));
        }

        public __MicroComIDropTargetProxy(IntPtr nativePointer, bool ownsHandle) : base(nativePointer, ownsHandle)
        {
        }

        protected override int VTableSize => base.VTableSize + 4;
    }

    unsafe class __MicroComIDropTargetVTable : ModernFormsNext.WindowKit.Backend.MicroCom.MicroComVtblBase
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DragEnterDelegate(IntPtr @this, void* pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
        static int DragEnter(IntPtr @this, void* pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            IDropTarget __target = null;
            try
            {
                {
                    __target = (IDropTarget)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.DragEnter(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IDataObject>(pDataObj, false), grfKeyState, pt, pdwEffect);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DragOverDelegate(IntPtr @this, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
        static int DragOver(IntPtr @this, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            IDropTarget __target = null;
            try
            {
                {
                    __target = (IDropTarget)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.DragOver(grfKeyState, pt, pdwEffect);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DragLeaveDelegate(IntPtr @this);
        static int DragLeave(IntPtr @this)
        {
            IDropTarget __target = null;
            try
            {
                {
                    __target = (IDropTarget)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.DragLeave();
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        delegate int DropDelegate(IntPtr @this, void* pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect);
        static int Drop(IntPtr @this, void* pDataObj, int grfKeyState, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT pt, DropEffect* pdwEffect)
        {
            IDropTarget __target = null;
            try
            {
                {
                    __target = (IDropTarget)ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.GetObjectFromCcw(@this);
                    __target.Drop(ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.CreateProxyFor<IDataObject>(pDataObj, false), grfKeyState, pt, pdwEffect);
                }
            }
            catch (System.Runtime.InteropServices.COMException __com_exception__)
            {
                return __com_exception__.ErrorCode;
            }
            catch (System.Exception __exception__)
            {
                ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.UnhandledException(__target, __exception__);
                return unchecked((int)0x80004005u);
            }

            return 0;
        }

        public __MicroComIDropTargetVTable()
        {
            base.AddMethod((DragEnterDelegate)DragEnter);
            base.AddMethod((DragOverDelegate)DragOver);
            base.AddMethod((DragLeaveDelegate)DragLeave);
            base.AddMethod((DropDelegate)Drop);
        }

        static internal void __MicroComModuleInit() => ModernFormsNext.WindowKit.Backend.MicroCom.MicroComRuntime.RegisterVTable(typeof(IDropTarget), new __MicroComIDropTargetVTable().CreateVTable());
    }

    class LocalInterop
    {
        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, void* arg1, void* arg2, void* arg3, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, uint arg0, void* arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, uint arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, ushort arg0, void* arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, void* arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, int arg0, ushort arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, int arg0, void* arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, IntPtr arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, ushort arg0, void* arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, ushort arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, void* arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, int arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, FILEOPENDIALOGOPTIONS arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, int arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* methodPtr)
        {
            throw null;
        }

        static unsafe public uint CalliStdCalluint(void* thisObj, uint arg0, void* arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public uint CalliStdCalluint(void* thisObj, uint arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public uint CalliStdCalluint(void* thisObj, void* arg0, void* arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public uint CalliStdCalluint(void* thisObj, void* arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public uint CalliStdCalluint(void* thisObj, void* arg0, void* arg1, int arg2, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, int arg1, void* arg2, void* arg3, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, int arg0, int arg1, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, DropEffect arg0, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, void* arg0, int arg1, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT arg2, void* arg3, void* methodPtr)
        {
            throw null;
        }

        static unsafe public int CalliStdCallint(void* thisObj, int arg0, ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop.UnmanagedMethods.POINT arg1, void* arg2, void* methodPtr)
        {
            throw null;
        }
    }
}