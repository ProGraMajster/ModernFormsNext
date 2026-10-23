# Page and shell authoring in Designer

Designer authors the stabilized `ContentPage`, `PageHost`, `NavigationPage`, `TabbedPage`,
`FlyoutPage` and empty `AppShell` inside existing Form or UserControl documents. Windows is
the primary Designer host. Android remains experimental; a Windows preview does not certify
Android rendering, Back, IME, accessibility or physical-device behavior.

## Authored ownership

The existing `DesignControlNode.Children` collection owns authored nodes. There is no separate
page graph, navigation stack, selection service or semantic tree in the document.

| Container | Authored children | Generated runtime ownership |
| --- | --- | --- |
| ContentPage | Zero or one control | `Content = child` |
| PageHost | Zero or one known page | `Page = child` |
| NavigationPage | Zero or one known root page | `new NavigationPage(root)` |
| TabbedPage | Ordered known pages | `Pages.Add(page)`, then `SelectedIndex` |
| FlyoutPage | At most one page for each explicit slot | `Flyout = page`, `Detail = page` |
| AppShell | None | Empty shell; routes remain application code |

Known pages are the exact built-in concrete types, using short or `ModernFormsNext`-qualified
names. Custom page subclasses are not inferred from names or loaded from project assemblies.
Constructor-owned presentation frames, tab buttons, internal hosts and navigation history are
real runtime controls, but never become authored nodes or generated fields.

Select a container and open **AuthoredContent** in PropertyGrid to edit its collection. The
existing collection dialog keeps edits detached until OK, preserves existing nodes and their
types, and applies one transaction. Flyout/Detail slots are explicit. ContentPage can contain a
Panel with ordinary Toolbox-authored descendants. Toolbox, outline moves and paste reject
occupied or incompatible slots. Cancel, rejected edits, undo and redo preserve canonical identity.
Removing or reordering a tab keeps an explicitly selected retained page where possible.

**Title** is the single editor for a page title. It reads legacy authored Text, and editing it
replaces that alias atomically. The reverse parser preserves the last literal Text/Title source
assignment. TabbedPage selection is validated against authored pages, rather than an empty
temporary runtime instance. Flyout metrics, enum values and durations use the real framework
setters for validation; invalid edits roll back without an undo unit.

`TransitionDuration` is entered in invariant milliseconds, with `0.0001` ms representing one
100-nanosecond tick. Persistence uses a canonical signed Int64 **string** in the sole `Ticks`
member of a `System.TimeSpan` value, preserving the full range through JSON. Page transitions
require non-negative durations. The generator emits `new global::System.TimeSpan(ticksL)`.

## Bounded preview and source safety

Page preview builds a short-lived, detached tree of trusted controls from the already-loaded
framework assembly. The production slot setters, layout and paint code determine geometry,
implicit children, selected-page visibility and clipping. Designer uses that geometry for its
existing selection and hit testing; it paints the composed page tree once. DPI and zoom scale
the logical result once through the existing painter.

The preview admits at most **256 authored nodes and depth 16** per page subtree. Unknown project
controls, unsupported values and over-budget subtrees produce explicit diagnostics/placeholders.
They are not reported as a successful preview. An authored custom UserControl remains an
independent component boundary; page preview does not instantiate it. The neutral graph preflight
also rejects cycles, shared ownership, null collections, more than 10,000 nodes or depth over 96
before recursive normalization or generation. An outer Designer transaction validates its final
graph before publishing history and remains rollbackable on failure.

Design-mode PageHost paints its current real frame while suppressing appearance/lifecycle
activation. All temporary controls, including hidden retained pages, are disposed. Preview does
not install application event handlers, bindings, route tables/factories, guards, lifecycle
bindings, delegates or converters. AppShell stays empty. Open-flyout intent and navigation
history are runtime state, rather than persisted preview state.

Control type names are parsed as complete bounded C# named types. Qualified names, `global::`,
aliases and supported generic arguments remain verbatim; statements, constructor expressions,
directives, comments, skipped text and malformed types are rejected. Enum source names and
nested structured values use the same boundary. Invalid source names fail generation with
diagnostics and **empty code**, rather than returning a partially usable initializer. Names are
never repaired by silently stripping input, and validation does not resolve or execute types.
Structured object types still use the existing explicit literal allow-list.
Informational source-name and hash header comments escape C# line terminators, including Unicode
line/paragraph separators, while leaving the caller's stored metadata unchanged.

The reverse parser accepts declared authored references for the known ownership members and a
declared root argument for NavigationPage. Factories, calls, lambdas, inline object initializer
graphs, guards and unsupported source remain explicit diagnostics; they are never evaluated.

## Optional example and startup

ControlGallery's **Designer page content** page hosts
`samples/ControlGallery/Examples/DesignedPagesView.mfdesign`, its generated `.Designer.cs`, and
a small UserControl partial. It demonstrates a retained editor in one tab and a constructor-owned
NavigationPage root in another. Open the companion document in the Designer to edit its authored
tree; switch tabs in Gallery to check retained runtime state.

The default application template and `ModernFormsNext.DemoApp` keep the recommended
`Application.Run(new MainForm())` startup. Pages need no additional application runtime. A designed
UserControl can be ordinary Form content, or the content of an application-owned ContentPage:

```csharp
// In the optional Gallery example's namespace, on the UI thread:
var view = new ControlGallery.Examples.DesignedPagesView { Dock = DockStyle.Fill };
form.Controls.Add(view); // The Form owns the view and its retained page tree.
```

Page-derived project root classes are not a new Designer document kind. Register routes,
parameters, factories, lifecycle callbacks and [AppShell lifecycle bindings](app-shell-routing.md)
explicitly in application code. See [page ownership and Back](pages-and-navigation.md),
[tab/flyout composition](page-composition.md), and [Designer transactions](designer-transactions-and-undo.md).
Native Windows, emulator, physical-device, human IME/TalkBack/keyboard and interactive Visual
Studio observations remain separate qualification lanes.
