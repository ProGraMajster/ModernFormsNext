# Shared virtualization certification

Issue [#203](https://github.com/ProGraMajster/ModernFormsNext/issues/203) certifies the
production `ItemsControl` and opt-in `ListView` added in #202. The reusable workload
is `samples/ModernFormsNext.CrossPlatform.Sample/VirtualizationCertification.cs`.
The Windows native test host links that same source. It creates two synthetic,
finite sources with 100,003 entries, using the production generator, layout,
scrollbars, focus, selection and logical accessibility peers.
Initial sources are prepared before the fixture is attached, as in application
startup. Indexed mutations are then exercised against the attached native host.

The explicit runners check native keyboard input, focus retirement/recovery,
stable logical peers, indexed collection mutations, reentrant selection, both stack
directions, both wrap directions, nested reveal, resize/visibility, variable metric
corrections, font and theme changes. The Windows runner delivers `WM_DPICHANGED`
through the real HWND at 96/144/192 DPI and checks device conversion. Android uses
its actual density and native orientation/configuration delivery. A Windows DPI
message is an automated backend check; it does not certify moving a physical
window between monitors. Android orientation is restored after the runner.

After 500 warmup steps, 120 batches of 50 deterministic jumps each alternate both
consumers. One-second intervals between batches return execution to the native
render/input loop. The complete workload has 6,500 jump pairs and lasts at least
two minutes. The runners require real native paint progress, bounded live/pool/
created counts, no further fixed-list container creation after warmup, correct
realized bindings and deterministic disposal of the live rows.

Memory checkpoints record forced-GC managed bytes, process private/working bytes,
live/pool/create/reuse counts, measured item count, corrected content extent and
last layout time. Batch allocation counts include synchronous production work on
the UI thread and exclude checkpoint/report formatting and intervening paint work.
The new native stability gate rejects retained managed growth of 16 MiB or more
after warmup. Process/native memory is recorded separately; this short, finite
soak does not establish an unlimited-duration native leak guarantee. Numeric
measurement metadata still scales with the indexed source capacity. The existing
pure zero-allocation assertions and original native fixture remain unchanged.

## Native accessibility membership

Android projection confirms canonical parent/child membership before accepting a
native node. For the default virtualized ListView peer, its existing internal
adapter can answer from stable item identity and the bounded visual collection.
It does not enumerate or create every preceding logical peer for a focus,
scrollbar or structure notification. A checked index hint on an already requested
logical peer is revalidated after mutations. Private scrollbar infrastructure
remains outside the semantic tree. Custom accessibility projections keep their
existing enumerated membership checks. There is no new public accessibility API,
parallel tree or alternate selection/focus model.
Default visual ancestors validate membership directly against their existing
control collection, including the Form client-area projection. This avoids
repeated indexed enumeration of the same siblings during row recycling. Controls
with other logical children and custom parent peers retain their enumeration
fallback; independently hidden custom children remain excluded.

The Android identity/subscription registry retires unselected, unfocused rows after
they leave this projection. A native integer ID that has retired is rejected;
re-entering the viewport registers the existing canonical logical peer again.
Selected/focused peer identity survives recycling. Membership does not recompute
full screen geometry when the exact canonical peer's `View` already rejects
invisible/disposed owners. Derived/custom peers retain their independent state
checks, including an `Invisible` state reported separately from `View`.

Android native node queries also project the realized/cache range and at most two
retained selected/focused logical peers, using their existing identities. Collection
metadata reports the entire indexed source count and logical row indices. Native
text search follows this current projection. Canonical hit testing considers the
same bounded visible candidates; unrealized rows have empty bounds. Custom peer
projections retain their previous semantics. Explicit public enumeration of every
logical child still scales with the requested source; this runner does not claim
a complete human TalkBack traversal benchmark.

Windows UIA structure-event bookkeeping uses the same bounded peer projection;
source changes invalidate children because a viewport snapshot cannot classify an
edit elsewhere in the logical source. Native first/last/sibling navigation still
addresses the entire flat ListView source by its checked logical indices, and
single selection queries use the existing selected peer. These queries do not
enumerate or create all preceding peers. Custom projections retain their fallback.

## Designer

The existing framework-only runtime preview uses at most twelve synthetic ListView
entries and a preview cache buffer capped at two. It uses the production layout
and renderer, including at Designer DPI/zoom. The temporary preview control and
its owned recycled rows are disposed after each paint. Authored `ItemSize`, layout,
orientation, cache and variable-size settings remain in the document unchanged;
synthetic entries and the preview cap never enter generated C#.

Project constructors, source indexers, template delegates and bindings are not
invoked for metadata or preview. Custom project controls retain their existing
metadata/placeholder boundary. Runtime-only selection, count and realized-range
properties stay hidden from serialization. Regression coverage follows Property
Grid edits through `.mfdesign`, the C# generator/parser, compilation and production
runtime realization; it also checks canonical undo/redo and incompatible settings.

## Repeat the native lanes

Build/test serially using `-m:1 /p:UseSharedCompilation=false`. The Windows test is
`WindowsVirtualizationCertificationTests`; the existing shorter
`WindowsNativeVirtualizationTests` remains a separate regression. To capture the
full report directly after building the Debug host:

```powershell
dotnet .\ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost\bin\Debug\net10.0-windows\ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost.dll --virtualization-certification
```

Build the standalone Android sample through the existing Android tooling, install
the signed APK on an explicitly selected emulator/device, then run:

```powershell
# Resolve adb through scripts/android/Resolve-Adb.ps1 when it is not on PATH.
adb -s <serial> shell am instrument -w com.programajster.modernformsnext.sample/com.programajster.modernformsnext.sample.VirtualizationValidationInstrumentation
adb -s <serial> pull /sdcard/Android/data/com.programajster.modernformsnext.sample/files/virtualization-validation.txt <local-report>
```

Require `ANDROID_VIRTUALIZATION_PASS` in the report and a successful instrumentation
result. Keep the emulator and physical report, model/API/fingerprint, exact source
SHA/tree, APK hash and configuration separately. An installed APK or desktop test
is not Android runtime evidence. Instrumentation and HWND checks are automated
evidence; human visual inspection, physical pointer/keyboard observations and
interactive Visual Studio checks must be labeled separately. Local logs, TRX,
reports and packages belong in ignored `artifacts/release-1.12.0/issue-203/`.
