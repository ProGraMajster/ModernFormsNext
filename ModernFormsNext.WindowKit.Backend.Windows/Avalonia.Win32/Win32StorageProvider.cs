
using ModernFormsNext.WindowKit.Backend.MicroCom;
using ModernFormsNext.WindowKit.Backend.Windows.Win32.Interop;
using ModernFormsNext.WindowKit.Backend.Windows.Win32Com;
using ModernFormsNext.WindowKit.Platform.Storage;
using ModernFormsNext.WindowKit.Platform.Storage.FileIO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace ModernFormsNext.WindowKit.Backend.Windows.Win32
{
    internal class Win32StorageProvider : BclStorageProvider
    {
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        private const FILEOPENDIALOGOPTIONS DefaultDialogOptions = FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_NOVALIDATE |
            FILEOPENDIALOGOPTIONS.FOS_NOTESTFILECREATE | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;

        private readonly WindowImpl _windowImpl;

        public Win32StorageProvider(WindowImpl windowImpl)
        {
            _windowImpl = windowImpl;
        }

        public override bool CanOpen => true;

        public override bool CanSave => true;

        public override bool CanPickFolder => true;

        public override async Task<IReadOnlyList<IStorageFolder>> OpenFolderPickerAsync(FolderPickerOpenOptions options)
        {
            var files = await ShowFilePicker(
                true, true,
                options.AllowMultiple, false,
                options.Title, null, options.SuggestedStartLocation, null, null);
            return files.Select(f => new BclStorageFolder(new DirectoryInfo(f))).ToArray();
        }

        public override async Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        {
            var files = await ShowFilePicker(
                true, false,
                options.AllowMultiple, false,
                options.Title, null, options.SuggestedStartLocation,
                null, options.FileTypeFilter);
            return files.Select(f => new BclStorageFile(new FileInfo(f))).ToArray();
        }

        public override async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
        {
            var files = await ShowFilePicker(
                false, false,
                false, options.ShowOverwritePrompt,
                options.Title, options.SuggestedFileName, options.SuggestedStartLocation,
                options.DefaultExtension, options.FileTypeChoices);
            return files.Select(f => new BclStorageFile(new FileInfo(f))).FirstOrDefault();
        }

        private Task<IEnumerable<string>> ShowFilePicker(
            bool isOpenFile,
            bool openFolder,
            bool allowMultiple,
            bool? showOverwritePrompt,
            string? title,
            string? suggestedFileName,
            IStorageFolder? folder,
            string? defaultExtension,
            IReadOnlyList<FilePickerFileType>? filters)
        {
            return RunStaDialogAsync(() => ShowFilePickerCore(
                isOpenFile,
                openFolder,
                allowMultiple,
                showOverwritePrompt,
                title,
                suggestedFileName,
                folder,
                defaultExtension,
                filters));
        }

        private static Task<T> RunStaDialogAsync<T>(Func<T> action)
        {
            var taskSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Windows Common Item Dialogs expect an STA thread with OLE initialized.
            // A regular ThreadPool worker can be MTA and makes COM failures depend on caller context.
            var thread = new Thread(() =>
            {
                var oleInitialized = false;
                try
                {
                    var oleResult = UnmanagedMethods.OleInitialize(IntPtr.Zero);
                    if (oleResult is not UnmanagedMethods.HRESULT.S_OK and not UnmanagedMethods.HRESULT.S_FALSE)
                    {
                        throw new COMException("OleInitialize failed.", unchecked((int)(uint)oleResult));
                    }

                    oleInitialized = true;
                    taskSource.SetResult(action());
                }
                catch (Exception ex)
                {
                    taskSource.SetException(ex);
                }
                finally
                {
                    if (oleInitialized)
                    {
                        UnmanagedMethods.OleUninitialize();
                    }
                }
            })
            {
                IsBackground = true,
                Name = "ModernFormsNext Win32 file dialog"
            };

#pragma warning disable CA1416 // Win32StorageProvider is only constructed by the Windows backend.
            thread.SetApartmentState(ApartmentState.STA);
#pragma warning restore CA1416
            thread.Start();

            return taskSource.Task;
        }

        private unsafe IEnumerable<string> ShowFilePickerCore(
            bool isOpenFile,
            bool openFolder,
            bool allowMultiple,
            bool? showOverwritePrompt,
            string? title,
            string? suggestedFileName,
            IStorageFolder? folder,
            string? defaultExtension,
            IReadOnlyList<FilePickerFileType>? filters)
        {
            IEnumerable<string> result = Array.Empty<string>();
            try
            {
                var clsid = isOpenFile ? UnmanagedMethods.ShellIds.OpenFileDialog : UnmanagedMethods.ShellIds.SaveFileDialog;
                var iid = UnmanagedMethods.ShellIds.IFileDialog;
                var frm = UnmanagedMethods.CreateInstance<IFileDialog>(ref clsid, ref iid);
                try
                {
                    var options = frm.Options;
                    options |= DefaultDialogOptions;
                    if (openFolder)
                    {
                        options |= FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS;
                    }
                    if (allowMultiple)
                    {
                        options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;
                    }

                    if (showOverwritePrompt == false)
                    {
                        options &= ~FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;
                    }
                    frm.SetOptions(options);

                    if (defaultExtension is null)
                    {
                        defaultExtension = String.Empty;
                    }

                    fixed (char* pExt = defaultExtension)
                    {
                        frm.SetDefaultExtension(pExt);
                    }

                    suggestedFileName ??= "";
                    fixed (char* fExt = suggestedFileName)
                    {
                        frm.SetFileName(fExt);
                    }

                    title ??= "";
                    fixed (char* tExt = title)
                    {
                        frm.SetTitle(tExt);
                    }

                    if (!openFolder)
                    {
                        var filtersPointer = FiltersToPointer(filters, out var count, out var filterStringPointers);
                        try
                        {
                            frm.SetFileTypes((uint)count, (void*)filtersPointer);
                            if (count > 0)
                            {
                                frm.SetFileTypeIndex(1);
                            }
                        }
                        finally
                        {
                            FreeFiltersPointer(filtersPointer, filterStringPointers);
                        }
                    }

                    if (folder?.TryGetLocalPath() is { } folderPath)
                    {
                        var riid = UnmanagedMethods.ShellIds.IShellItem;
                        if (UnmanagedMethods.SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref riid, out var directoryShellItem)
                            == (uint)UnmanagedMethods.HRESULT.S_OK)
                        {
                            using var proxy = MicroComRuntime.CreateProxyFor<IShellItem>(directoryShellItem, true);
                            frm.SetFolder(proxy);
                            frm.SetDefaultFolder(proxy);
                        }
                    }

                    var showResult = frm.Show(_windowImpl.Handle.Handle);

                    if ((uint)showResult == (uint)UnmanagedMethods.HRESULT.E_CANCELLED)
                    {
                        return result;
                    }
                    else if ((uint)showResult != (uint)UnmanagedMethods.HRESULT.S_OK)
                    {
                        throw new Win32Exception(showResult);
                    }

                    if (allowMultiple)
                    {
                        using var fileOpenDialog = frm.QueryInterface<IFileOpenDialog>();
                        using var shellItemArray = fileOpenDialog.Results;
                        var count = shellItemArray.Count;

                        var results = new List<string>();
                        for (int i = 0; i < count; i++)
                        {
                            using var shellItem = shellItemArray.GetItemAt(i);
                            if (GetAbsoluteFilePath(shellItem) is { } selected)
                            {
                                results.Add(selected);
                            }
                        }

                        result = results;
                    }
                    else
                    {
                        using var shellItem = frm.Result;
                        if (shellItem is not null && GetAbsoluteFilePath(shellItem) is { } singleResult)
                        {
                            result = new[] { singleResult };
                        }
                    }

                    return result;
                }
                finally
                {
                    frm.Dispose();
                }
            }
            catch (COMException ex)
            {
                var message = new Win32Exception(ex.HResult).Message;
                throw new COMException(message, ex.HResult);
            }
        }


        private static unsafe string? GetAbsoluteFilePath(IShellItem shellItem)
        {
            var pszString = new IntPtr(shellItem.GetDisplayName(SIGDN_FILESYSPATH));
            if (pszString != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStringUni(pszString);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pszString);
                }
            }
            return default;
        }

        private static IntPtr FiltersToPointer(
            IReadOnlyList<FilePickerFileType>? filters,
            out int length,
            out List<IntPtr> filterStringPointers)
        {
            if (filters == null || filters.Count == 0)
            {
                filters = new List<FilePickerFileType>
                {
                    FilePickerFileTypes.All
                };
            }

            var size = Marshal.SizeOf<NativeFilterSpec>();
            var result = Marshal.AllocCoTaskMem(size * filters.Count);
            filterStringPointers = new List<IntPtr>(filters.Count * 2);

            try
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    var filter = filters[i];
                    var namePointer = Marshal.StringToCoTaskMemUni(filter.Name);
                    filterStringPointers.Add(namePointer);

                    var patternPointer = Marshal.StringToCoTaskMemUni(GetFilterPattern(filter));
                    filterStringPointers.Add(patternPointer);

                    var native = new NativeFilterSpec
                    {
                        pszName = namePointer,
                        pszSpec = patternPointer
                    };

                    Marshal.StructureToPtr(native, IntPtr.Add(result, i * size), false);
                }
            }
            catch
            {
                FreeFiltersPointer(result, filterStringPointers);
                throw;
            }

            length = filters.Count;
            return result;
        }

        private static string GetFilterPattern(FilePickerFileType filter)
        {
            return filter.Patterns is { Count: > 0 }
                ? string.Join(";", filter.Patterns)
                : "*.*";
        }

        private static void FreeFiltersPointer(IntPtr filtersPointer, List<IntPtr> filterStringPointers)
        {
            foreach (var filterStringPointer in filterStringPointers)
            {
                if (filterStringPointer != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(filterStringPointer);
                }
            }

            if (filtersPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(filtersPointer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFilterSpec
        {
            public IntPtr pszName;
            public IntPtr pszSpec;
        }
    }
}
