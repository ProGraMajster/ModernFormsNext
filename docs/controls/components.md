# Components and dialogs

These public application-facing types are in the `ModernFormsNext` assembly
and NuGet package. The namespace is `ModernFormsNext` unless shown otherwise.
The [control catalog](README.md) also lists the four window types; a Form
is a window root with a control tree.

| Type | Base or kind | Use and runtime boundary |
| --- | --- | --- |
| [`Timer`](timer.md) | Component | Periodic UI-dispatcher work. Own and stop it with the view. |
| [`ToolTip`](tool-tip.md) | Component | Hover and programmatic help. See the native popup and inactive-window rules. |
| [`ImageList`](image-list.md) | Component | Shared image keys and indices for controls. |
| [`ModernFormsNext.DataBinding.BindingSource`](binding-source.md) | Component | Data binding and current-item currency. |
| [`NotifyIcon`](../platform-specific-features.md#notifyicon) | Component | Windows taskbar notification area. |
| `NotifyIconContextMenu` | Component | Owns the tray icon menu's item collection. |
| `NotifyIconMenuItem` | Component | Tray menu entry with click, command and check state. |
| `NotifyIconMenuSeparatorItem` | NotifyIconMenuItem | Visual separator for Windows tray actions. |
| `ModernFormsNext.Help.HelpProvider` | Component | Adds help strings and target metadata to controls. |
| `ColorDialog` | Standalone dialog object | Awaits managed color selection from a Form. |
| `BrushEditDialog` | Standalone dialog object | Awaits the managed brush editor. Assign Brush before showing and consume it only on OK. |
| `FontDialog` | CommonDialog | Edits font choice. System rendering needs a backend with the corresponding native service. |
| `OpenFileDialog` | FileDialog | Awaits file selection; AllowMultiple selects one or more files. |
| `SaveFileDialog` | FileDialog | Awaits the destination from the owner's storage provider. The application writes its own data. |
| `FolderBrowserDialog` | FileSystemDialog | Awaits folder selection and exposes the selected storage item. |
| `PrintDocument` | Component | Owns print settings and print-page events. Keep drawing usable for both preview and print. |
| `PrintDialog` | CommonDialog | Awaits printer and range configuration. |
| `PageSetupDialog` | CommonDialog | Awaits paper, orientation and margins configuration. |
| [`SystemMessageBox`](../android-platform-services.md#system-messages-and-framework-messages) | Static service | Requests a backend message dialog. Check the implementation on the target platform. |

The storage dialog returns a `DialogResult` and exposes selected storage
objects as well as available path projections. Check the result before using
a selection, and use the backend storage objects if the target does not
guarantee an ordinary local path. Windows is the primary platform. Android is
experimental and individual dialog capabilities depend on its Activity host
and registered services.

`CommonDialog`, `FileDialog` and `FileSystemDialog` define the shared base
contracts. Models such as `MenuItem`, `ListViewItem`, `TabStripItem`, font and
printer settings, resources and geometry are documented in the associated
guide and API reference. Designer tray support is determined by metadata; keep
runtime services and callbacks in code.
