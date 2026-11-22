# Control and component catalog

Browse the code-first guides by category. Each row names a public type in the
`ModernFormsNext` assembly and NuGet package. The namespace is the prefix
of its full name. The base type identifies inherited layout, input and ownership
contracts. See [control fundamentals](control-fundamentals.md) for shared APIs and
example setup.

This catalog covers every concrete public `Control` type in the framework
assembly, including public base-like controls, and provides separate guidance
for the four window types. `NavigationHeader` is obtained from its owning
navigation host. The [components and dialogs inventory](components.md) lists
the remaining application-facing components. Abstract bases such as `Shape`,
`Page`, `ItemsControl`, `ScrollBar` and `MenuBase` have conceptual guides and
the generated API reference. Model and item types, Designer UI chrome and
backend peers are outside this inventory.

Windows is the primary supported runtime. **Experimental** marks Android
coverage. Qualification is scenario-specific and documented in the
[Android support matrix](../platforms/android.md). The standard Visual Studio
Designer uses design data and metadata. Toolbox and property support vary by
type.

## Actions and selection

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.Button` | `Control` | Windows / experimental Android | [Button](button.md) |
| `ModernFormsNext.CheckBox` | `Control` | Windows / experimental Android | [CheckBox](check-box.md) |
| `ModernFormsNext.ComboBox` | `Control` | Windows / experimental Android host | [ComboBox](combo-box.md) |
| `ModernFormsNext.RadioButton` | `Control` | Windows / experimental Android | [RadioButton](radio-button.md) |
| `ModernFormsNext.Switch` | `Control` | Windows / experimental Android | [Switch](switch.md) |
| `ModernFormsNext.TrackBar` | `Control` | Windows / experimental Android | [TrackBar](track-bar.md) |

## Text and input

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.DateTimePicker` | `Control` | Windows / experimental Android host | [DateTimePicker](date-time-picker.md) |
| `ModernFormsNext.Label` | `Control` | Windows / experimental Android | [Label](label.md) |
| `ModernFormsNext.LinkLabel` | `Label` | Windows / experimental Android | [LinkLabel](link-label.md) |
| `ModernFormsNext.MarkdownEditor` | `Panel` | Windows / experimental Android | [MarkdownEditor](markdown-editor.md) |
| `ModernFormsNext.MaskedTextBox` | `TextBox` | Windows / experimental Android | [MaskedTextBox](masked-text-box.md) |
| `ModernFormsNext.NumericUpDown` | `Control` | Windows / experimental Android | [NumericUpDown](numeric-up-down.md) |
| `ModernFormsNext.RichTextBox` | `TextBox` | Windows / experimental Android | [RichTextBox](rich-text-box.md) |
| `ModernFormsNext.SearchBar` | `Control` | Windows / experimental Android host | [SearchBar](search-bar.md) |
| `ModernFormsNext.TextBox` | `ScrollControl` | Windows / experimental Android | [TextBox](text-box.md) |
| `ModernFormsNext.TimePicker` | `Control` | Windows / experimental Android | [TimePicker](time-picker.md) |

## Lists and data

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.DataBinding.BindingNavigator` | `FlowLayoutPanel` | Windows / experimental Android | [BindingNavigator](binding-navigator.md) |
| `ModernFormsNext.CarouselView` | `Control` | Windows / experimental Android | [CarouselView](carousel-view.md) |
| `ModernFormsNext.CheckedListBox` | `ListBox` | Windows / experimental Android | [CheckedListBox](checked-list-box.md) |
| `ModernFormsNext.CollectionView` | `ItemsControl` | Windows / experimental Android | [CollectionView](collection-view.md) |
| `ModernFormsNext.DataGridView` | `Control` | Windows / experimental Android | [DataGridView](data-grid-view.md) |
| `ModernFormsNext.ListBox` | `Control` | Windows / experimental Android | [ListBox](list-box.md) |
| `ModernFormsNext.ListView` | `ItemsControl` | Windows / experimental Android | [ListView](list-view.md) |
| `ModernFormsNext.RefreshView` | `ScrollableControl` | Windows / experimental Android | [RefreshView](refresh-view.md) |
| `ModernFormsNext.TreeView` | `Control` | Windows / experimental Android | [TreeView](tree-view.md) |

## Layout and containers

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.Control` | `Component` | Windows / experimental Android | [Control](control.md) |
| `ModernFormsNext.FlowLayoutPanel` | `Panel` | Windows / experimental Android | [FlowLayoutPanel](flow-layout-panel.md) |
| `ModernFormsNext.GroupBox` | `Control` | Windows / experimental Android | [GroupBox](group-box.md) |
| `ModernFormsNext.HorizontalScrollBar` | `ScrollBar` | Windows / experimental Android | [HorizontalScrollBar](horizontal-scroll-bar.md) |
| `ModernFormsNext.Panel` | `ScrollableControl` | Windows / experimental Android | [Panel](panel.md) |
| `ModernFormsNext.ScrollControl` | `Control` | Windows / experimental Android | [ScrollControl](scroll-control.md) |
| `ModernFormsNext.ScrollableControl` | `Control` | Windows / experimental Android | [ScrollableControl](scrollable-control.md) |
| `ModernFormsNext.SplitContainer` | `Control` | Windows / experimental Android | [SplitContainer](split-container.md) |
| `ModernFormsNext.Splitter` | `Control` | Windows / experimental Android | [Splitter](splitter.md) |
| `ModernFormsNext.TableLayoutPanel` | `Panel` | Windows / experimental Android | [TableLayoutPanel](table-layout-panel.md) |
| `ModernFormsNext.UserControl` | `ScrollableControl` | Windows / experimental Android | [UserControl](user-control.md) |
| `ModernFormsNext.VerticalScrollBar` | `ScrollBar` | Windows / experimental Android | [VerticalScrollBar](vertical-scroll-bar.md) |

## Pages and navigation

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.AppShell` | `Page` | Windows / experimental Android | [AppShell](app-shell.md) |
| `ModernFormsNext.ContentPage` | `Page` | Windows / experimental Android | [ContentPage](content-page.md) |
| `ModernFormsNext.FlyoutPage` | `Page` | Windows / experimental Android | [FlyoutPage](flyout-page.md) |
| `ModernFormsNext.NavigationHeader` | `Control` | Windows / experimental Android | [NavigationHeader](navigation-header.md) |
| `ModernFormsNext.NavigationPage` | `Page` | Windows / experimental Android | [NavigationPage](navigation-page.md) |
| `ModernFormsNext.NavigationPane` | `Control` | Windows / experimental Android | [NavigationPane](navigation-pane.md) |
| `ModernFormsNext.PageHost` | `Control` | Windows / experimental Android | [PageHost](page-host.md) |
| `ModernFormsNext.TabControl` | `Control` | Windows / experimental Android | [TabControl](tab-control.md) |
| `ModernFormsNext.TabPage` | `Panel` | Windows / experimental Android | [TabPage](tab-page.md) |
| `ModernFormsNext.TabStrip` | `Control` | Windows / experimental Android | [TabStrip](tab-strip.md) |
| `ModernFormsNext.TabbedPage` | `Page` | Windows / experimental Android | [TabbedPage](tabbed-page.md) |

## Menus and toolbars

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.ContextMenu` | `MenuDropDown` | Windows / experimental Android host | [ContextMenu](context-menu.md) |
| `ModernFormsNext.Menu` | `MenuBase` | Windows / experimental Android | [Menu](menu.md) |
| `ModernFormsNext.MenuDropDown` | `MenuBase` | Windows / experimental Android host | [MenuDropDown](menu-drop-down.md) |
| `ModernFormsNext.Ribbon` | `Control` | Windows / experimental Android | [Ribbon](ribbon.md) |
| `ModernFormsNext.StatusBar` | `Control` | Windows / experimental Android | [StatusBar](status-bar.md) |
| `ModernFormsNext.ToolBar` | `MenuBase` | Windows / experimental Android | [ToolBar](tool-bar.md) |

## Media and geometry

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.Circle` | `Ellipse` | Windows / experimental Android | [Circle](circle.md) |
| `ModernFormsNext.ColorBox` | `Control` | Windows / experimental Android | [ColorBox](color-box.md) |
| `ModernFormsNext.DocumentViewer` | `ScrollControl` | Windows / experimental Android | [DocumentViewer](document-viewer.md) |
| `ModernFormsNext.Ellipse` | `Shape` | Windows / experimental Android | [Ellipse](ellipse.md) |
| `ModernFormsNext.HueSlider` | `Control` | Windows / experimental Android | [HueSlider](hue-slider.md) |
| `ModernFormsNext.Line` | `Shape` | Windows / experimental Android | [Line](line.md) |
| `ModernFormsNext.MarkdownViewer` | `DocumentViewer` | Windows / experimental Android | [MarkdownViewer](markdown-viewer.md) |
| `ModernFormsNext.Path` | `Shape` | Windows / experimental Android | [Path](path.md) |
| `ModernFormsNext.PictureBox` | `Control` | Windows / experimental Android | [PictureBox](picture-box.md) |
| `ModernFormsNext.Polygon` | `Shape` | Windows / experimental Android | [Polygon](polygon.md) |
| `ModernFormsNext.Polyline` | `Shape` | Windows / experimental Android | [Polyline](polyline.md) |

## Windows and integration

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.Form` | `WindowBase` | Windows / experimental Android host | [Form](form.md) |
| `ModernFormsNext.FormTitleBar` | `Control` | Windows / experimental Android host | [FormTitleBar](form-title-bar.md) |
| `ModernFormsNext.MessageBoxForm` | `Form` | Windows / experimental Android host | [MessageBoxForm](message-box-form.md) |
| `ModernFormsNext.NativeViewHost` | `Control` | Windows HWND / experimental Android View | [NativeViewHost](native-view-host.md) |
| `ModernFormsNext.PopupWindow` | `WindowBase` | Windows / experimental Android host | [PopupWindow](popup-window.md) |
| `ModernFormsNext.PrintPreviewControl` | `Control` | Windows printing | [PrintPreviewControl](print-preview-control.md) |
| `ModernFormsNext.PrintPreviewDialog` | `Form` | Windows printing | [PrintPreviewDialog](print-preview-dialog.md) |
| `ModernFormsNext.ProgressBar` | `Control` | Windows / experimental Android | [ProgressBar](progress-bar.md) |

## Components

| Public type | Base type | Runtime path | Guide |
| --- | --- | --- | --- |
| `ModernFormsNext.DataBinding.BindingSource` | `Component` | Windows / experimental Android | [BindingSource](binding-source.md) |
| `ModernFormsNext.ImageList` | `Component` | Windows / experimental Android | [ImageList](image-list.md) |
| `ModernFormsNext.Timer` | `Component` | Windows / experimental Android | [Timer](timer.md) |
| `ModernFormsNext.ToolTip` | `Component` | Windows / experimental Android host | [ToolTip](tool-tip.md) |
