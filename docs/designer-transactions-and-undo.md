# Designer transactions and undo/redo

The ModernFormsNext Designer records authored model edits as atomic transactions. The active
`DesignerSession` exposes one `DesignerTransactionManager`, while every open `.mfdesign` document
owns an independent bounded `DesignerHistory`. The transaction API belongs to the Designer
assembly; it does not change the runtime framework or the `.mfdesign` format.

## Model

An outer transaction becomes one `DesignerUndoUnit`. The unit contains typed `IDesignerChange`
objects for root values, control-node values, serialized property and event dictionaries, tree
insert/remove/move operations, ordered child replacement, or document replacement. Each change
stores an explicit previous and forward value and implements deterministic `Apply` and `Revert`.
Undo never derives its target by reversing the current model and does not serialize the entire
document for an ordinary edit.

The one deliberate broader capture is a design-root resize. The production layout engine persists
Anchor-derived bounds for existing descendants, so the operation records direct root and node
fields for that existing tree. This remains an in-memory typed changeset, not a serialized document
snapshot. It makes root resize, rollback, undo, runtime layout, and generated output agree.

Tree changes retain node object identity. Deleting a container therefore retains its subtree only
while the corresponding undo/redo unit is reachable. `ClearHistory`, document close, history-limit
eviction, and `DesignerSession.Dispose` release retained changes and selections. History is never
written to disk.

## Transaction lifecycle

Begin a transaction with a concise user-facing description and commit it only after every step
succeeds:

```csharp
using var transaction = session.Transactions.Begin("Change Padding");

session.SetPropertyValue(
    control,
    "Padding",
    DesignerPropertyValueEditor.ToDesignPropertyValue(padding, typeof(Padding)));

transaction.Commit();
```

Disposal without `Commit` performs deterministic rollback. An explicit `Rollback` is useful for a
dialog Cancel or an invalid multi-step edit. If code throws before commit, the `using` scope restores
all changes already recorded in that transaction and creates no history entry.

Complex Property Grid dialogs share one outer editor transaction. **OK** records one unit and
**Cancel** records none. The Interaction Effects dialog treats **Apply** as in-dialog working state
inside that same transaction: a later Cancel still restores the pre-dialog value, while OK
commits the final applied value once.

Session mutation helpers start a transaction when called on their own and join an existing
transaction when used in a batch:

```csharp
using var transaction = session.Transactions.Begin("Move 4 controls");

foreach (var control in controls)
    session.SetNodeBounds(control, CalculateFinalBounds(control));

transaction.Commit();
```

The available helpers cover names, bounds, serialized properties, property reset, ordered children,
and root resize. New Designer editors should use these helpers or record a focused
`DesignerModelMutationSnapshot` around an existing descriptor setter. They must not mutate the model
and then call `NotifyDocumentChanged`: that compatibility method cannot reconstruct previous values,
so it clears potentially stale history and marks the document dirty.

## Nested transactions and coalescing

A nested commit joins its changes to the outer unit. A nested rollback reverts only changes made
since that nested scope began. The outer description and selection boundary become the user-visible
history item. Committing the outer transaction creates no item when all recorded changes are no-ops.

Interactive move, resize, root resize, and splitter gestures open one transaction on pointer down,
apply live model values during pointer movement, then record the final changes and commit on pointer
up. Repeated changes to the same typed target merge inside that transaction. Escape records the
current live state and rolls it back, so a hundred pointer moves still produce either one undo unit
or no unit after cancellation. Future snapping and multi-selection work can contribute more typed
changes to the same gesture without changing that rule.

## History, dirty state, and save

`CanUndo`, `CanRedo`, `UndoDescription`, and `RedoDescription` describe the active document. Undo
moves the latest unit to redo; redo moves it back. A new effective commit clears redo. Undo and redo
are rejected while a transaction is active.

History uses monotonically assigned revision identities rather than stack length as the modified
flag. Saving calls `MarkSaved`, which marks the current revision. Undo or redo back to exactly that
revision makes `DesignerSession.IsDirty` false; moving before or after it makes the document dirty.
Each open document keeps its own revision and stacks. Normal load/reopen starts empty and clean, and
constructing a document never emits history entries.

`ModernFormsDesignerOptions.HistoryLimit` defaults to 500 entries per open document and must be at
least one. Oldest units are evicted when the combined undo/redo retention exceeds the limit. If an
evicted save position can no longer be reached, the reachable revisions remain dirty. Call
`session.Transactions.ClearHistory()` for recovery scenarios; it clears both stacks while preserving
the current dirty status.

Auto-save, explicit save, serialization, and C# generation run only after an outer commit or replay
notification and refuse to run inside an active transaction. History stores model values and node
references, never generated C#, delegates, native handles, runtime Skia resources, or platform
objects. Generated `.Designer.cs` output is always derived from the current committed model.

## Replay and notifications

The manager reports `Recording`, `Undoing`, `Redoing`, and `RollingBack` modes. During undo/redo or
rollback, mutation paths can refresh derived model state but recording is suppressed, preventing
recursive history entries from property-change observers. The suppression remains active while the
central model notification refreshes layout, rendering, hit testing, Document Outline, Property
Grid, selection handles, and dirty UI.

The low-frequency events are `TransactionCommitted`, `TransactionRolledBack`, `UndoPerformed`,
`RedoPerformed`, and `HistoryChanged`. They are intended for UI refresh and command enablement, not
for maintaining a second history. An observer exception after a completed commit, rollback, undo,
or redo does not reactivate the transaction, corrupt the history stacks, or leave replay
suppression enabled.

Transactions have affinity to the thread that created `DesignerSession`, normally the Designer UI
dispatcher. Cross-thread begin/mutation and document switching during a transaction fail with a
clear exception. No background synchronization or persistent history is provided.

## Editor integration checklist

When adding an editor or mutation path:

1. Start one outer transaction with a user-visible description.
2. Record only the fields and collections that operation owns.
3. Let nested helpers join the outer transaction.
4. Record partially applied direct mutations before propagating an exception.
5. Roll back on Cancel, invalid output, parser/generator failure, or Escape.
6. Commit once after validation; do not save or generate during the transaction.
7. Add headless apply/revert, selection, no-op, exception, and deterministic output tests.

Do not introduce an `Action`-lambda undo stack, serialize whole documents for small edits, store
transient runtime resources, create separate history for layout/render refresh, or emit an entry for
selection alone.

Inherited design remains tracked separately. The typed transaction boundary is ready for a future
change validator to reject writes to base-owned/read-only nodes before `Apply`; this issue does not
implement inherited controls. Detached Designer Copy/Cut/Paste/Duplicate reuses this boundary as
documented in [Designer copy and paste](designer-copy-paste.md). Multi-selection, an operating-system
clipboard contract, smart guides, and a general command framework remain separate features.
