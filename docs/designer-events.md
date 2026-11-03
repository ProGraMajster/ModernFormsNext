# Designer events and code navigation

The Events view stores handler **names**, rather than executable delegates, in `.mfdesign`.
The existing C# generator emits each non-empty binding once in `InitializeComponent`.
Handler bodies belong in the paired user file, for example `MainForm.cs`, never in
`MainForm.Designer.cs`.

## Create, assign, navigate and clear

Double-click a control on the design surface to activate its default event. The root Form
uses `Load`; common controls use their conventional event, such as `Button.Click` or
`TextBox.TextChanged`. Safe custom-control metadata can specify `DefaultEventAttribute`.
An absent or unsupported default produces a diagnostic and leaves the document unchanged.
Multi-selection does not create a handler.

In Properties, switch to **Events**. Double-click an empty event value to create its default
handler name. Double-click an existing binding to validate it and navigate to its method.
Press Enter on a selected row to edit its value, type a new name and press Enter to commit,
or use the adjacent **…** button to choose a compatible existing method. The chooser also
provides **Clear binding (keep user methods)**. Escape cancels the text editor. Merely moving
focus to the chooser does not commit unfinished text.

For example, activating `saveButton.Click` creates a user method such as:

```csharp
private void saveButton_Click(object? sender, global::System.EventArgs e)
{
    // TODO: Add event handling logic.
}
```

The signature follows the event's supported void delegate. Roslyn checks the same instance
method-group conversion used by generated subscriptions, including aliases, compatible
overloads and reference variance. A same-named field, static method, incompatible return or
parameter type, unresolved signature or ambiguous overload is rejected instead of being
mistaken for a valid handler. Ref/out, pointer, ref-like and non-void custom delegates remain
unsupported by automatic handler generation.

The current editor targets one matching, non-generic, top-level class and namespace in the
paired `.cs` file. The chooser lists methods with bodies in that class, including expression
bodies. Methods in other partial files, inherited methods, generic methods and classes with
conditional directives require source editing. The Designer does not guess another class.

## Transactions and conflicts

Source inspection and insertion complete before the Designer commits one ordinary binding
transaction. No model transaction remains open while the host handles a code request.
Bounded Roslyn inspection runs on one worker per shell over immutable text and detached class
identity; it never reads controls or calls editor services from that worker. Canceled queued
analysis does not start another concurrent compiler for the same shell.
Selection, document, path or intervening model changes invalidate pending work. A denied edit,
syntax error or stale source snapshot preserves the previous binding and undo history.

Unbinding and Designer Undo/Redo change only the authored binding. They never delete user
methods. The code editor owns its own source undo and save history. If cancellation or a late
conflict occurs after a source insertion has already completed, the new user method is retained
and no stale binding is applied. There is no cross-file atomic undo transaction.

A missing or renamed method is reported in the selected event's description and Designer
output. Inspect an existing binding by selecting or activating its row. Diagnostics do not
silently rename, remove or recreate it. Choose a current compatible method or clear the
binding explicitly. Save/load retains the authored names even when source needs repair.

## Host integration

`IDesignerHostEnvironment` remains unchanged. Hosts can additionally implement
`IDesignerCodeDocumentHostEnvironment` to read current source, insert a method against an
exact expected snapshot and navigate to a UTF-16 code location. Requests start on the Designer
UI thread; hosts must marshal their own editor work without synchronously waiting on either
application's dispatcher. A host rejection never falls back to disk.

The Visual Studio extension uses its existing per-pane pipe and state polling. Code requests
are correlated, bounded, restricted to the current `.mfdesign` document's paired user `.cs`
path and expire after 30 seconds. The pipe accepts only the current Windows user. Visual Studio
reads its live text buffer, including unsaved text, queries edit permission and rechecks the
current buffer and document after that callback. It inserts through the native text buffer;
the normal VS undo, dirty indicator and user save path remain responsible for user code.
Navigation validates the source snapshot before placing the caret.

Standalone hosts without that optional capability use a bounded UTF-8 file insertion preserving
BOM and newline style, and report the path when navigation is unavailable. Save the design
document first. Other file encodings require a live editor host. Concurrent external changes
detected before insertion are rejected. The atomic replacement also retains and checks the
actual replaced bytes. If a save races the final comparison, it restores that user version and
retains the other version in a named `.mfn-event-conflict-*.tmp` file for explicit review. If
restoration fails, the diagnostic identifies retained recovery paths. No binding commits on
that conflict, and recovery files are not automatically deleted. Keep source editing in one
host while using standalone generation. No handler is written into an application installation
directory for an unsaved design.

## Non-executing analysis and budgets

Event analysis reads C# syntax, Roslyn symbols and PE metadata. It does not load project
assemblies into the Designer runtime, evaluate MSBuild, instantiate project attributes or
controls, call getters/converters, or execute delegates. Existing preview and runtime layout
semantics are unchanged.

Hard inspection budgets reject uncertain work with a diagnostic: 512 Ki UTF-16 characters per
source/buffer, 8 Mi characters and 512 source files per project inspection, 8192 enumerated
project entries, syntax nesting 128, 1024 tokens per uninterrupted expression/declaration,
2048 root members, 128 chooser method names, 256 handler-name characters and 32 event parameters. Project metadata XML
is limited to 4 Mi characters and restored package JSON to 16 MiB. Additional PE images are
limited to 16 MiB each and 64 MiB combined; total reference identities are capped at 1024.
Transport frames are limited to 4 Mi characters, queued operations to eight and method
insertions to 16 Ki characters. Larger or conditional projects can continue using source
editing; these limits do not constrain application runtime events or control counts.

Contract and buffer-adapter tests are automated evidence. A successful build or an isolated
host test does not establish end-to-end navigation in a real Visual Studio installation;
that observation must be recorded separately in qualification results.
