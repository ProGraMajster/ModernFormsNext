# Native control validation

ModernFormsNext validation v1 connects existing keyboard focus ownership to existing
data binding. It is a native framework feature, not a full WinForms validation clone.
It introduces no `ContainerControl`, `AutoValidate`, `ValidationConstraints`, separate
focus manager, binding engine, or IME route.

## Automatic departure

For a voluntary focus request from A to B, where B has `CausesValidation == true`:

1. `A.Validating` runs on the UI thread while A remains the canonical owner and
   retains its text-input session. All public observers run before validation writes.
2. If the resulting `CancelEventArgs.Cancel` is true, stop.
3. Snapshot A's `OnValidation` bindings and process them in collection order through
   the existing `Binding.PullData` conversion/write pipeline. Stop on first failure.
4. Raise `A.Validated` while A still owns focus. Recheck the request after callbacks.
5. Commit the canonical owner/selection flags, perform the existing text-input
   handoff, raise `A.LostFocus`, then the still-current `B.GotFocus`.

No owner, no-op selection, or destination `CausesValidation == false` skips departure
validation. `CausesValidation` defaults to true. It is a destination policy, not a
ban on explicitly validating that control. It has no change event in this minimal API.

```csharp
editor.Validating += (_, e) => e.Cancel = string.IsNullOrWhiteSpace(editor.Text);
editor.Validated += (_, _) => status.Text = "Accepted";
cancelButton.CausesValidation = false;

// Default OnValidation; BindingContext belongs to the control tree.
fields.BindingContext = new ModernFormsNext.DataBinding.BindingContext();
editor.DataBindings.Add(nameof(TextBox.Text), model, nameof(model.Name), true);
```

Cancellation, failed conversion/write, or stale request suppresses the focus commit
and `Validated` (unless invalidation occurs inside that notification itself). There
is no Lost/Got pair or text handoff from the rejected request. A canceled pointer
departure does not start capture/click on B or finish A's composition. A click inside
the current editor still finishes composition before caret placement.

Mandatory retirement (hide/disable/remove/detach/dispose of owner or ancestor, window
close/disposal, surface disposal) bypasses validation. A disposed owner cannot veto
retirement. Native activation and Android screen-reader accessibility focus remain
separate from canonical keyboard ownership. Form, windowless surface, Tab, pointer,
accessibility keyboard focus and automation all enter the same focus preflight.

## Explicit validation and traversal

`Control.Validate()` executes the same observer/binding/completion routine without
requesting focus or transferring text input. `CausesValidation` does not affect it.
It returns true only after successful completion. Live detached, hidden and disabled
controls can validate; disposed/retiring controls and recursive validation of the
same control return false. Call on the owning UI thread. Exceptions from public
validation observers propagate, with the active-validation guard released for retry.

`Control.ValidateChildren()` validates explicit descendants. `WindowBase.ValidateChildren()`
(therefore also Form) uses its public `Controls`; Form's client controls are included
while its implicit title bar is excluded. A surface's application root is already a
Control: call `surface.Root.ValidateChildren()` or `surface.Root.Validate()` as needed.

```csharp
saveButton.CausesValidation = false; // Save explicitly validates all application fields.
saveButton.Click += (_, _) =>
{
    if (form.ValidateChildren()) SaveModel();
};
```

Traversal snapshots the tree at entry, visits parents before children in public
collection order, and excludes the receiver itself. Hidden/disabled fields are
included, as are fields without bindings/handlers. Implicit controls and their
subtrees are excluded. Disposed or moved snapshot entries are skipped, including an
entry moved away and back. Added children wait for the next call. The first canceled
or failed control returns false; observer exceptions propagate and stop traversal.
A changed focus generation or invalidated traversal root stops the operation.

OnValidation bindings present in the traversal snapshot retain pending field values
until each field's turn. This prevents a source notification from an earlier field's
setter from erasing edits on later fields sharing that source. Protection is released
before that field's public `Validating` observers; the normal post-observer binding
snapshot then applies. Cancellation or an exception releases protection on all
unvisited fields, so subsequent source refreshes work normally.

The explicit APIs themselves never move focus. Application callbacks can do so;
such a focus change invalidates the unfinished explicit operation. A nested attempt
to validate the same active control is rejected rather than recursively raising events.

## Binding semantics and errors

Control bindings no longer subscribe to `Validating` through TypeDescriptor. Their
internal participation occurs **after** all public handlers accept, regardless of
whether binding creation preceded or followed user subscription. Generic non-Control
bindable components retain their discovered `Validating` event and legacy behavior.

Only `OnValidation` bindings join this phase. `OnPropertyChanged` keeps its immediate
writes and is not written again by validation. `Never` is not written. Explicit
`WriteValue()` still forces the existing pull and `ReadValue()` forces the existing
push without requiring validation; application calls to these APIs inside a public
observer are intentional operations, not deferred validation writes.

The binding snapshot is taken after `Validating`: additions, removals, mode changes
and BindingSource changes made by those observers participate. During binding writes,
new bindings wait until the next attempt; removed or no-longer-OnValidation bindings
are skipped. Source notifications cannot automatically push/pull over pending values
of snapshot participants. Explicit ReadValue/WriteValue still work. This prevents an
earlier setter's notification from erasing another field before its turn. Pending
state is always released, including on failure. Protection applies only while a
participant remains in OnValidation mode: changing it to OnPropertyChanged restores
immediate property updates even within a validation callback.

The same parser/formatter, null substitution, ControlUpdateMode, BindingSource and
CurrencyManager are used. Identity/lifetime/currency checks between callback phases
reject stale writes, including source replacement from Parse and retirement during
IEditableObject.BeginEdit. A fieldless list binding may replace its current item through
the existing currency setter. Changes already made by arbitrary callbacks cannot be undone.

- A public `Validating` exception propagates before any validation binding write.
- Parse/conversion failure rejects native validation, including legacy formatting-disabled
  bindings. Existing reformat behavior is preserved; the latter may restore source text.
- With formatting enabled, setter/conversion errors still report through BindingComplete.
  Native validation rejects error states even if an observer clears Cancel. Completion
  cancellation also rejects. A throwing BindingComplete observer retains the existing
  binding behavior that marks completion canceled.
- Without formatting, source setter exceptions propagate. Target getter exceptions
  propagate; the in-flight binding flag is released even when the getter fails.
- IDataErrorInfo errors reject native validation. Dirty state remains for a later retry.
- `Validated` exceptions propagate before focus commit. Successful setters preceding
  that exception have already run.

This is **not an ACID model transaction**. Bindings run fail-fast in collection order.
If binding 1 writes and binding 2 fails, binding 1's arbitrary setter effects may remain;
the framework cannot roll them back. BindingComplete cancellation/error can likewise
arrive after a setter has executed. Focus remains at A unless application code explicitly
caused another accepted transition or required retirement.

## Reentrancy

During automatic `Validating`, conversion/write callbacks, and `Validated`, nested
voluntary selections that also need validation are coalesced into one latest valid
destination. They do not recursively revalidate A or commit before the observer has
returned its cancellation decision. Accepted completion submits that destination once
through the canonical transaction with the completed departure approval. Cancellation
rejects the entire pending departure. Selecting A again cancels the pending departure.

A nested `CausesValidation == false` request may commit immediately; mandatory
retirement also runs immediately. Both invalidate the earlier attempt. Hidden, disposed,
removed or reparented destinations cannot receive an obsolete commit. Owner, ancestry,
generation and destination are checked again after public and binding callback phases.
`Select()` is void; a reentrant semantic action requesting deferred focus can report
rejection at that instant even if its destination wins when validation returns.

## Verification boundaries

Regression coverage includes both roots, event snapshots, handler order, cancellation,
failure modes, multiple bindings, dynamic mutations, traversal, reentrancy, input routing,
session retention, automation and inherited Designer event discovery. Designer uses
normal runtime metadata; no hard-coded validation event list was introduced.

The existing Windows UI automation host accepts `--validation` for real-HWND success
and cancellation with both system and custom decorations and the Windows text method.
It uses framework APIs, does not force OS foreground focus, and is not a manual CJK
IME test. Shared surface and Android backend tests are not emulator/device qualification.
Android-specific production code, templates, and public inheritance remain unchanged.
