# Form size constraint notifications (#165)

`Form.MinimumSize` and `Form.MaximumSize` describe the drawable window's limits in
logical pixels. A setter can coerce the opposite limit to retain a coherent pair.
Previously that second change was silent. Both changed properties now publish through
their existing events and protected hooks, without adding a second geometry API.

```csharp
form.MinimumSize = new Size(200, 200);
form.MinimumSizeChanged += (_, _) => UpdateMinimumDisplay(form.MinimumSize);
form.MaximumSizeChanged += (_, _) => UpdateMaximumDisplay(form.MaximumSize);

form.MaximumSize = new Size(100, 100);
// Both limits are now 100 x 100. MaximumSizeChanged precedes MinimumSizeChanged.
form.MinimumSize = new Size(300, 300);
// Both limits are now 300 x 300. MinimumSizeChanged precedes MaximumSizeChanged.
```

Set constraints on the UI thread. The backend receives the final pair before managed
fields commit, preserving rejection behavior. Normal size clamping then runs through
the existing resize/layout path. Constraint observers see the committed pair and
current native size. `Size.Empty` continues to clear a limit; the existing empty and
per-axis semantics are unchanged. Repeating a value is silent.

The explicitly set property is notified first, followed by a coerced partner only if
its effective value changed. A newer constraint assignment supersedes remaining old
notifications, including later subscribers to the same event. A generation check also
handles callbacks that change the pair and then restore its previous values. No-op
callbacks do not invalidate the notification batch. Close or dispose stops remaining
notifications, including when invoked by a resize callback during clamping.

Observer failures do not roll back accepted constraints. Still-current subscribers and
the paired property are notified before a single failure is rethrown, or multiple
failures are reported as `AggregateException`. Resize/layout callback failures follow
the same rule after the pair has committed. Derived event hooks must call base to
deliver notifications through the guarded subscriber path.

## Qualification

`FormSizeConstraintNotificationTests` contains 23 headless cases covering both coercion
directions, backend handoff, single/no-op/empty changes, size clamping, nested and
same-value callbacks, restored pairs, exceptions and close/dispose. Before the fix,
18 of the initial 21 cases failed and three passed. Two additional protected-hook
regressions reproduced stale publication when an override reentered before calling
base; the notification context now preserves the originating generation across the
virtual call. All 23 cases pass after that correction. Red and green TRX evidence
are retained separately.

The existing native window geometry scenario additionally exercises both directions
and callback-driven close on real owned HWNDs with framework and system decorations.
It compares observable size with `GetClientRect` and verifies notification order. This
is automated native validation, not manual monitor/DPI or visual qualification.
Full-suite and CI results are recorded in the implementing PR.

No public member, renderer, window-state contract, platform abstraction, startup or
template is replaced. ControlGallery and the default generated application do not
need changes for this event/constraint correction.
