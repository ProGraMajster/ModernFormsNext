# Issue #161: native validation implementation and evidence

Implementation baseline: `984fbca99a77f165bae36459e2eae131dcf68c04` after #160.
Feature branch: `codex/issue-161-validation`. This revision includes the final review
for a dedicated PR to `master`; merge is outside the publication task.
The public contract and examples are in [validation.md](../validation.md).

## Audit and implementation decisions

1. **Root cause.** Binding defaults to OnValidation and discovered a Validating event through
   TypeDescriptor, but Control had no validation lifecycle. The initial regression failed on
   both Form and surface: edited text did not reach the model after focus departure.
2. **Engine.** One internal `Control.ValidateCore` executes observer, binding and completion
   phases. Explicit calls, traversal and focus preflight share it.
3. **Preflight.** `ControlFocusScope.Preflight` invokes departure validation. Canonical owner
   writes remain exclusively in the existing Request commit; forced retirement bypasses it.
4. **Order.** Validating -> OnValidation PullData writes/BindingComplete -> Validated ->
   canonical focus commit -> existing text handoff -> LostFocus -> current GotFocus.
5. **Cancellation.** A canceled observer runs no validation binding writes. Failure retains
   the old owner/session and suppresses completion/focus events from that request. Callback
   exceptions propagate; explicitly requested newer bypass/forced transitions are retained.
6. **CausesValidation.** Existing default state bit is true. The destination controls automatic
   departure validation. False does not disable explicit validation. No change event was needed.
7. **Binding integration.** Control's snapshot runs after all public observers accept. Control
   bindings no longer subscribe to Validating, eliminating subscription-order dependence.
8. **Generic components.** Non-Control components retain TypeDescriptor discovery and their
   existing Target_Validate path, verified with a real BindableComponent and one source write.
9. **Other modes.** OnPropertyChanged remains immediate; Never does not write automatically.
   Neither participates in the native validation write phase.
10. **Explicit binding operations.** WriteValue/ReadValue still force the existing pipeline,
    independently of focus or validation. They are intentional even during callbacks.
11. **Failures.** Parse/conversion and IDataErrorInfo errors reject native validation. With
    formatting, BindingComplete still reports errors; clearing Cancel cannot turn an error
    state into accepted validation. Without formatting, setter exceptions propagate. Getter
    exceptions propagate and release the in-flight flag. Existing null/format conversion is reused.
12. **Multiple bindings.** Collection-order snapshot, fail-fast, no ACID rollback. Successful
    earlier setters may remain. Automatic source refresh/pull is suppressed on pending snapshot
    participants so the first write cannot erase later input. Explicit binding operations remain.
13. **Validate placement.** Control owns the field and its DataBindings, so `Control.Validate()`
    is the smallest natural API. Live detached/hidden/disabled controls work; disposed/retiring
    controls and same-control recursion return false. UI thread affinity is enforced.
14. **ValidateChildren placement.** Every Control is a container. Control and WindowBase expose
    traversal over their public Controls; Form automatically uses its client-area children.
    No inheritance change or public ContainerControl was introduced.
15. **Traversal.** Snapshot, depth-first preorder, public collection order, receiver excluded,
    hidden/disabled fields included, implicit subtrees excluded. Moved/disposed entries are
    skipped and additions deferred. First failure stops; exceptions propagate.
16. **Reentrancy.** Validation keeps one latest pending validating destination until the entire
    departure is accepted. Cancellation rejects it; selecting the owner cancels departure.
    Bypass requests and forced retirement act immediately and invalidate old work. Version,
    ancestry and identity checks suppress stale Validating/Validated/binding continuations.
17. **Retirement.** Hide, disable, remove, detach, dispose, ancestor and window/surface retirement
    remain unconditional. They do not validate and cannot be vetoed by a disposed owner.
18. **Text input.** The old session survives preflight/cancellation. Only an accepted canonical
    commit invokes ControlTextInputHost handoff. Surface pointer composition finishing was moved
    behind the departure decision; a click within the active editor preserves its prior behavior.
19. **Roots.** Form/ControlAdapter and SkiaControlSurface share the engine and regression matrix.
20. **Input/automation.** Select, Tab, pointer, accessibility keyboard focus and automation use
    preflight. Rejected/throwing pointer down cannot turn into a release click. Android screen-reader
    focus remains separate. No backend keyboard focus route was added.
21. **Designer.** Runtime inherited event metadata exposes Validating/Validated naturally. A
    Designer Events-view regression verifies discovery without a hard-coded list or UI change.
22. **Files.** Complete changed-file inventory is below, including this evidence document.
23. **New public surface.** Control.Validating, Validated, CausesValidation, Validate(),
    ValidateChildren(), protected OnValidating/OnValidated; WindowBase.ValidateChildren().
    All have XML documentation; examples and policy documentation accompany them.
24. **Tests.** New coverage and complete test-method inventory are listed below. The existing
    #160 focus, binding/TestHost, text input, surface, accessibility and automation suites remain.
25. **Builds.** Final Debug and Release outcomes are recorded in the evidence table below.
26. **Full suite.** Final Debug totals are recorded below. No thresholds/retries/skips were changed.
27. **Native Windows.** `UiAutomationHost --validation` verifies real HWND, actual Windows text
    method, accepted write/handoff and cancellation/session retention, with both decoration modes.
28. **Android/shared surface.** Shared validation is covered on the surface; existing Android
    backend tests run as part of the full suite. No Android-specific production code changed.
29. **Device/manual boundary.** Android emulator, physical device, manual CJK IME, interactive
    Visual Studio and manual ControlGallery: NOT EXECUTED. These are not claimed by unit/native
    API smoke results. Template verification is not required: template/startup content is unchanged.
30. **Git hygiene.** Diff/status/source-hash checks are recorded below. Local logs/TRX stay in
    ignored artifacts. The primary checkout's untracked .codex/config.toml is preserved unchanged.
31. **Compatibility/risk.** Additive public API, but the previously inert default OnValidation
    now writes and can prevent departure on errors. Model side effects are not rolled back.
    Nested semantic focus requests may report rejection while their deferred destination later
    wins. Explicit reads/writes and arbitrary observer side effects remain application-controlled.
32. **Deferred scope.** #162–#165, DPI icon, native hosting, Android windows/services, GPU,
    other desktop backends, WebView/media, full WinForms parity, ContainerControl, AutoValidate,
    ValidationConstraints, navigation and ErrorProvider redesign remain outside this change.
33. **Readiness.** The final review fixes and validation evidence are recorded below, with
    the pre-existing intermittent brush allocation qualification risk retained. The dedicated
    PR closes #161 only after merge; #144 and #146 remain neutrally related umbrella issues.

## Changed files

| File | Purpose |
| --- | --- |
| `ModernFormsNext/Control.Validation.cs` | Public API, canonical routine and traversal |
| `ModernFormsNext/ControlFocusScope.Validation.cs` | Existing preflight integration and pending redirect |
| `ModernFormsNext/ControlFocusScope.cs` | Wire preflight and coalesce requests during validation |
| `ModernFormsNext/WindowBase.Validation.cs` | Natural public window traversal |
| `ModernFormsNext/DataBinding/Binding.cs` | Reuse PullData with native validation participation/guards |
| `ModernFormsNext/DataBinding/Binding.BindToObject.cs` | Revalidate after BeginEdit callback |
| `ModernFormsNext/Control.cs` | Report accepted/rejected pointer down to routers |
| `ModernFormsNext/WindowBase.cs` | Suppress release click after rejected down |
| `ModernFormsNext/SkiaControlSurface.cs` | Preserve canceled composition; cancel rejected/throwing gestures |
| `ModernFormsNext.Testing.Tests/ValidationTests.cs` | Both-root fixture and basic write regression |
| `ModernFormsNext.Testing.Tests/ValidationTests.Focus.cs` | Order, cancellation, retirement, reentrancy, input/session tests |
| `ModernFormsNext.Testing.Tests/ValidationTests.Binding.cs` | Modes, errors, mutations, multiple/generic/currency binding tests |
| `ModernFormsNext.Testing.Tests/ValidationTests.Traversal.cs` | Explicit API, tree snapshot, fail-fast and thread tests |
| `ModernFormsNext.Tests/ValidationTraversalTests.cs` | Implicit subtree exclusion |
| `ModernFormsNext.Automation.Tests/ActionTests.cs` | Keyboard focus cancellation/bypass on both roots |
| `ModernFormsNext.Designer.Tests/ValidationDesignerTests.cs` | Inherited event discovery and default metadata |
| `ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/ValidationScenario.cs` | Real HWND smoke |
| `ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/Program.cs` | --validation entry point |
| `docs/data-binding.md` | Working default binding lifecycle |
| `docs/text-input.md` | Preflight, pending requests and session contract |
| `docs/validation.md` | Public examples, policies and limitations |
| `docs/development/issue-161-validation-audit.md` | Audit, complete inventory and validation evidence |

## Regression inventory

The Form/surface matrix runs both roots unless a test concerns an inherently separate API.
Formatting, explicit/focus validation, failure stage, callback phase and mutation type expand
theories into independent cases. Tests exercise public paths rather than duplicating engine logic.

| Area | Test methods |
| --- | --- |
| Basic/order | FocusDepartureCommitsDefaultOnValidationBinding; EventsObservePrecommitOwnerThenExistingTextHandoff |
| Cancellation/input | CancellationPrecedesBindingsRegardlessOfSubscriptionOrder; InputCancellationKeepsCompositionAndDoesNotStartPointerGesture; BypassSkipsDepartureButExplicitValidationStillWorks; ThrowingPointerValidationCannotLeaveAReleaseClickAndNextGestureWorks |
| Reentrancy/retirement | CallbackReentrancyCannotCommitObsoleteDestination; RedirectThenCancelRejectsEntireDeparture; ForcedRetirementNeverValidates; OwnerReparentFromValidationRetiresSessionWithoutWriting; DestinationMovedAwayAndBackCannotResumeOldRequest |
| Text mutation | ValidatingCanChangeCompositionBeforeBindingReadsIt |
| Errors/modes | BindingFailureRejectsValidationAndPreservesSession; BindingCompleteCancellationStopsFocusAfterSetterSideEffect; SourceDataErrorCannotBeAcceptedByClearingCompletionCancel; OtherModesAndExplicitReadWriteKeepTheirContract |
| Bindings/mutation | MultipleBindingsAreOrderedFailFastWithNoRollback; ObserverBindingMutationsParticipateInPostObserverSnapshot; ParseCallbackCannotWriteThroughStaleBindingOrRequest; BindingPhaseUsesSnapshotAndSkipsRemovedBindings; PendingBindingModeChangePreservesImmediatePropertyUpdates |
| Existing pipeline | GenericComponentKeepsItsDiscoveredValidatingPathExactlyOnce; CurrencyManagerWritesCurrentItemAndPreservesOtherRows; FieldlessValueTypeListBindingUsesExistingCurrencySetter; NullSubstitutionAndControlUpdateNeverKeepExistingConversion; BeginEditInvalidationPreventsStaleSourceSetter |
| Explicit/traversal | ExplicitValidationDoesNotRequireFocusOrVisibilityAndRejectsRecursion; ExplicitCancellationAndExceptionKeepFocusAndAllowRetry; TraversalUsesPublicDescendantsPreorderIncludingHiddenDisabled; TraversalStopsAtFirstFailureWithEarlierWritesRetained; TraversalSnapshotSkipsMovedEntriesAndDefersAdditions; ExplicitFocusMutationAbortsRemainingTraversal; ExplicitValidationRequiresOwningThread; TraversalExcludesImplicitFrameworkControlsAndTheirSubtrees |
| Integration | FocusActionHonorsValidationCancellationAndDestinationBypass; InheritedValidationEventsAppearThroughRuntimeMetadata |
| Pending traversal fields | TraversalPreservesPendingValuesOnDifferentControlsSharingSource; TraversalFailureReleasesPendingRefreshOnUnvisitedFields |

## Final review corrections

Two local blockers were reproduced on both Form and surface before the corrections
(`review-probes.log` / TRX: four expected failures):

- An earlier field's successful setter could trigger a shared BindingSource refresh
  that overwrote a later field's uncommitted text during ValidateChildren. Traversal
  now protects the existing OnValidation participants until each field's turn, then
  releases its traversal protection before public observers and the same ValidateCore.
- Switching a pending binding to OnPropertyChanged inside BindingComplete still
  suppressed its immediate update. Pending protection now applies only in OnValidation
  mode. A depth counter supports overlapping field/traversal scopes without premature
  release; all scopes release in finally, including unvisited fields on cancel/error.

Eight additional regression cases cover both reproductions and cleanup after traversal
cancellation/exception. These changes stay within the existing engine and 22-file scope.

## Implementation validation before final review

Recorded 2026-10-04 using .NET SDK 10.0.401, sequential MSBuild (`-m:1` and
`/p:UseSharedCompilation=false`). Logs/TRX are local ignored artifacts under `artifacts/issue-161/`.

| Check | Result | Local evidence |
| --- | --- | --- |
| Initial defect reproduction before implementation | 2/2 failed as expected (unchanged source after departure) | `baseline.log`, `baseline.trx` |
| Restore | PASS | `restore.log` |
| Final Debug solution build | PASS, 0 warnings, 0 errors | `build-debug-final.log` |
| Release solution build | PASS, 0 warnings, 0 errors | `build-release.log` |
| Targeted final run | 230/230 PASS: 152 new validation cases + 59 canonical focus + 19 input reentrancy | `targeted-final.log`, `targeted-final.trx` |
| All new automated cases | 158/158 PASS: 152 validation + 1 implicit traversal + 1 Designer + 4 automation | Targeted and full confirmation TRX |
| Native Windows validation | PASS, system/custom decorations, real HWND and Windows text method | `native-windows.log` |
| Final complete Debug confirmation | 4065/4065 PASS, 0 skipped, 9 test projects | `full-debug-confirmation.log`, `full-debug-confirmation/*.trx` |
| git diff --check | PASS; new files also scanned for whitespace/conflict markers | Final local Git check |
| Source identity | All 18 changed C# files match the source SHA-256 snapshot used for final validation | `validated-source-hashes.json` |

Full confirmation breakdown:

| Project | Passed/total |
| --- | --- |
| ModernFormsNext.Automation.Tests | 166/166 |
| ModernFormsNext.Automation.Windows.Tests | 66/66 |
| ModernFormsNext.CrossPlatform.Sample.Tests | 29/29 |
| ModernFormsNext.Designer.Tests | 691/691 |
| ModernFormsNext.Testing.Tests | 919/919 |
| ModernFormsNext.Tests | 1482/1482 |
| ModernFormsNext.VisualStudioExtension.Vsix.Tests | 26/26 |
| ModernFormsNext.WindowKit.Backend.Android.Tests | 358/358 |
| ModernFormsNext.WindowKit.Backend.Windows.Tests | 328/328 |

Representative executed commands (from the worktree root):

```powershell
dotnet restore ModernFormsNext.slnx -m:1 /p:UseSharedCompilation=false
dotnet build ModernFormsNext.slnx --configuration Debug --no-restore -m:1 /p:UseSharedCompilation=false /p:EnableWindowsTargeting=true
dotnet build ModernFormsNext.slnx --configuration Release --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test ModernFormsNext.Testing.Tests/ModernFormsNext.Testing.Tests.csproj --configuration Debug --no-restore -m:1 /p:UseSharedCompilation=false --filter 'FullyQualifiedName~ValidationTests|FullyQualifiedName~CanonicalFocusTests|FullyQualifiedName~InputReentrancyTests'
dotnet ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost/bin/Debug/net10.0-windows/ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll --validation
dotnet test ModernFormsNext.slnx --configuration Debug --no-restore --no-build -m:1 /p:UseSharedCompilation=false
git diff --check
```

The actual test invocations additionally selected TRX log names/results directories;
all build/test console output was retained locally. This table records the earlier
implementation validation; the final review changed core code and therefore requires
the fresh qualification recorded below. GitHub Actions status belongs to the PR.

## Intermittent allocation test: retained failure evidence

The first complete implementation run passed 4053/4053 (before the final additional
validation cases). The next complete run had **4064/4065 PASS**, with only
`BrushInterpolationCompatibilityTests.PreparedPlanDoesNotAllocatePerIntermediateFrame`
failing: **6280 B versus the unchanged 256 B limit** (`full-debug-final.log` and TRX).
All #161 regressions passed in that run.

Evidence separating this from the validation feature:

- The retained #160 run, before #161, failed the same test at **496 B / 256 B**:
  `artifacts/issue-160/full-debug/Marcin_DESKTOP-U97OBPG_2026-10-03_20_06_08_net10.0.trx`.
- The test, `Animations/BrushAnimationPlan.cs`, and drawing sources have no diff
  against the #161 baseline. The measured Apply loop operates on local brush snapshots,
  not Control focus/validation or binding callbacks.
- Five independent isolated executions passed with the same binaries and unchanged
  threshold (`allocation-1` through `allocation-5`, logs/TRX).
- A subsequent complete run, with unchanged source/binaries/test settings, passed
  **4065/4065** (`full-debug-confirmation`). No test was skipped, weakened or edited.

This is evidence of a pre-existing intermittent qualification failure, not a proof of
the exact allocation source. That diagnosis remains a separate follow-up; no animation
scheduler/interpolator change or retry-policy change was made for #161.

## Final review qualification

After the two review corrections, the following checks were executed again on
2026-10-04 with the same sequential build policy. The 18 changed C# files match
`reviewed-source-hashes.json`; subsequent edits only record documentation/results.

| Check | Result | Local evidence |
| --- | --- | --- |
| Restore | PASS | `review-restore.log` |
| Debug solution build | PASS, 0 warnings, 0 errors | `review-build-debug.log` |
| Release solution build | PASS, 0 warnings, 0 errors | `review-build-release.log` |
| Targeted regression run | 238/238 PASS: 160 validation + 59 canonical focus + 19 input reentrancy | `review-targeted.log`, `review-targeted.trx` |
| All new automated cases | 166/166 PASS: 160 validation + 1 implicit traversal + 1 Designer + 4 automation | Targeted and full review TRX |
| Native Windows validation | PASS, real HWND and Windows text method, both decoration modes | `review-native-windows.log` |
| Full Debug suite | 4073/4073 PASS, 0 skipped, 9 projects | `review-full-debug.log`, `review-full-debug/*.trx` |
| Allocation regression | PASS in the complete review suite; test/limit unchanged | Full review TRX |
| Git hygiene | PASS, changed-line diff check and new-file scan | Final staged diff check |

Final suite breakdown: Automation 166/166; Automation.Windows 66/66;
CrossPlatform sample 29/29; Designer 691/691; Testing 927/927; Framework 1482/1482;
VSIX 26/26; Android backend 358/358; Windows backend 328/328.

The device/manual boundaries above still apply. The earlier allocation failure
remains part of the evidence and is not erased by this successful qualification.

## Publication boundaries

The single feature commit is limited to the 22 implementation/test/documentation
files listed above. Logs, TRX, source-hash snapshots, PR-body helpers and build/package
outputs remain local ignored artifacts. No package metadata, template, generated output
or `.codex/config.toml` belongs to this change. The primary checkout's local config is
preserved. The PR uses `Fixes #161`, `Related to #144` and `Related to #146`; publishing
the feature does not authorize merge or implementation of another issue.
