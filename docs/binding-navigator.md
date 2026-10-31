# BindingNavigator

`ModernFormsNext.DataBinding.BindingNavigator` is a code-first navigation and edit
bar built from ordinary ModernFormsNext buttons, a text editor and a label. It uses
the existing `BindingSource`, command, layout, focus, validation, accessibility and
theme services. It has no dependency on WinForms controls or designer serialization.

```csharp
using System.ComponentModel;
using ModernFormsNext;
using ModernFormsNext.DataBinding;

var people = new BindingSource
{
    DataSource = new BindingList<Person>
    {
        new() { Name = "Ada" },
        new() { Name = "Grace" }
    }
};
var navigator = new BindingNavigator(people) { Dock = DockStyle.Top };
form.Controls.Add(navigator);
// The application disposes people after its controls have detached.
```

Use the control and update its source on the UI thread. The navigator borrows its
source: replacement and disposal detach notifications without disposing the source
or its list. Disposing the source clears the navigator. When list notifications are
suppressed, call `RefreshItems()` after the batch. Balanced `BeginInit`/`EndInit`
calls can defer presentation updates during setup; scopes may nest.

The navigator itself reads source currency directly. If other editors/labels use
`DataBindings`, give their containing control a `BindingContext`, as in
`panel.BindingContext = new BindingContext()`, and use ordinary binding update modes.

## Standard behavior

| Element | Default behavior |
|---|---|
| First / Previous / Next / Last | Navigate the existing zero-based source currency; unavailable boundary actions are disabled. |
| Position | Display one-based currency, or zero for an empty/unbound list. Enter or ordinary successful validation commits integer input, clamped to the current list. Invalid input and Escape restore the current position. |
| Count | Display `CountItemFormat`, initially `of {0}`. |
| Add | Call `BindingSource.AddNew()` when adding is allowed, including on an empty list. |
| Delete | Call `RemoveCurrent()` when a current item exists and removal is allowed. |
| Save | Call `EndEdit()` for an editable or addable source's current item. This can commit a pending addition. |
| Cancel | Call `CancelEdit()` for an editable or addable source's current item. The item/list determines its supported rollback behavior. |

Save has **no persistence implementation**. Save/Cancel availability describes source
capabilities; it does not claim to observe an application dirty flag. Read-only lists
remain navigable while default editing actions are unavailable. Source/action
exceptions propagate without retry. The control refreshes still-current presentation
after an action failure; a callback that replaces the source or disposes the bar owns
the newer state.

Cancel has `CausesValidation = false`, using the canonical validation opt-out to let
an invalid draft be discarded. Other buttons retain ordinary validation behavior.
Tab and Shift+Tab use the existing logical control order. Public editor key observers
receive input before the built-in Enter/Escape handling. IME composition is left to
the existing text editor.

## Application actions and presentation

The read-only button properties expose the owned controls. Replace their normal
`Command` to configure add/delete/save/cancel behavior. A replacement command owns
its own availability policy and may use any `CommandParameter` or routed target;
source refresh never overwrites them. The command is borrowed and never disposed or
canceled by the navigator. Use the existing `AsyncCommand` for asynchronous work.
When a replacement command's predicate depends on source state, the application must
raise its `CanExecuteChanged` after that state changes, just as for any other command
source. The Gallery example explicitly requeries its save command on source changes.

```csharp
navigator.SaveButton.Command = new DelegateCommand(() =>
{
    people.EndEdit();
    SavePeople(); // Application-owned persistence and error handling.
}, () => people.Count > 0);
navigator.CancelButton.Text = "Discard draft";
navigator.DeleteButton.Visible = false;
```

Default commands contribute availability through `CommandSource`, preserving local
`Enabled` intent and ordinary inherited disabled state. Visibility remains with the
application. Customize owned controls without disposing or reparenting them.

The default bar docks at the top and uses one non-wrapping flow row with canonical
horizontal scrolling. Sizes are logical pixels; buttons use ordinary text measurement
and minimum sizes. For a narrow/mobile surface, set `WrapContents = true` and provide
enough `Height` for the desired rows. `RightToLeft` follows the shared FlowLayout
contract; currency, Tab order and semantic identities retain their logical order.
Existing control styles, inherited fonts and ThemeManager resources render each child.

`CountItemFormat` is localizable literal text with the exact `{0}` token. For example,
`Records: {0}` is supported. Other brace expressions, alignment and format specifiers
are rejected before formatting. This closed data template avoids disproportionate
formatting work in a Designer preview.

## Designer

The toolbox uses the full identity `ModernFormsNext.DataBinding.BindingNavigator`.
Ordinary scalar/layout properties, including `CountItemFormat`, support `.mfdesign`
and C# generation. Preview constructs only the closed framework control with a null
source; it never loads an application source, command or persistence callback.

`BindingSource`, the owned controls and their commands are hidden from Designer
serialization. Assign runtime references/customization after `InitializeComponent`.
The generated application uses ModernFormsNext's existing document/generator contract;
WinForms serialization hooks are not supported. The navigator's implicit presentation
children remain in the canonical runtime tree and are not authored document children.

ControlGallery has a BindingNavigator page with live currency, editing, read-only and
empty states, an application save hook, and RTL switching. Automated/native evidence
is recorded separately from human manual or screen-reader certification.
