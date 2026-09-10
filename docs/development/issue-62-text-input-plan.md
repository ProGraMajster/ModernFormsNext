# Issue #62 — composition audit and implementation plan

Audit date: 2026-09-10. Starting point: merged master
`5eb19a5098f5fdba794d57fe599cf0224f2ab4d8`, fetched again before this branch.
The full issue and all comments (zero) were reread. Its formal blocked-by list is
empty. This document records the plan **before production changes**.

## Existing implementation and actual gaps

Raw committed text already has a separate Windows `RawTextInputEventArgs` /
framework `KeyPress` path. The command resolver preserves AltGraph and separates
Android IME editing requests from hardware shortcuts. `TextBoxDocument` already
owns UTF-16 text, caret, selection, composition ranges and revision; the shared
Skia surface calls ordinary virtual editor operations. RichTextBox and the
Markdown source editor inherit this document/editor path. These remain canonical.

Windows composition hooks are commented out although the default composition
window is suppressed. Android already implements composing/commit/delete, but a
retained InputConnection can act on a different newly focused editor. Native
queries copy the complete text; input hints and editor actions are incomplete.
Focus changes, ancestor removal and callbacks during edits do not consistently
terminate old composition ownership. MaxLength truncation can split a surrogate
pair, and deletion work can depend on an unbounded native request without progress.

There is no explicit rollback operation distinct from Android's text-preserving
finish, shared caret geometry contract or extensible text-client seam. RichTextBox
has typed formatting runs but no general undo system. Markdown already has an edit
transaction depth and delta history; these must be reused for composition grouping.

History reviewed includes `ceef719` (document composition), `a043a94` (UTF-16/layout
conversion), `e6b711b` (render-aligned hit testing), `2050702` (hardware versus IME),
and PRs #22, #104, #106, #113 and #115. Current tests, Android sample, ControlGallery,
CHANGELOG, architecture/input docs, known limitations AND-04/TXT-01/TXT-02/TST-01
and roadmap agree that native language/device coverage is incomplete.

## Dependencies and scope

#56 is closed. The implemented #63 lifecycle and #64 TestHost scope is merged and
available. #86/#87 consume #62; their remaining rich editing, clipboard and large
document features are separate. #109 owns fuller Android hardware shortcut
mapping and follows this issue. #59 accessibility and #61 future diagnostics use
the same control/focus semantics. Excluded #60/#72/native products and #69 device
matrix are not implemented here. Native ownership handoff requires revocable
shared sessions, not placeholder native controls or another focus runtime.

## Additive architecture

1. Add platform-neutral `WindowKit.Input` contracts: a text client, immutable
   bounded text snapshot with document offset/length and absolute oriented ranges,
   logical caret rectangle, options and semantic composition notifications. Expose
   explicit commit, compose, region, selection, finish, cancel, surrounding deletion
   and editor-action operations. Reject invalid inputs before mutation. Snapshot
   requests have a hard maximum and preserve scalar boundaries; diagnostics never
   contain snapshot text. Document revision remains an edit/selection token, while
   state notifications also cover geometry and option changes.
2. Add a protected Control client-discovery extension. TextBox supplies a lazy
   adapter over its existing document and virtual edits. Custom controls may supply
   their own client without inheriting TextBox. No backend receives Control types.
3. A small per-host coordinator follows canonical selection and control ancestry.
   It lends a revocable client proxy to the native method. Retained proxies cannot
   retarget a new editor. Validate ownership before and after callbacks; revoke
   before focus/host/disposal notifications. Reuse existing focus traversal for
   Next/Previous actions. Native-host handoff can suspend/reacquire the same host.
4. Keep public surface text APIs and legacy Android events/providers compatible,
   delegating current controls to the common client. Add an optional native method
   capability through the existing window feature seam and explicit surface
   attachment. Software keyboard visibility reports whether a backend can honor it.
5. Composition updates use an editor-owned checkpoint for the replaced fragment.
   Cancel restores that fragment and oriented selection; RichTextBox additionally
   preserves affected typed runs and insertion style. Finish keeps visible text and
   drops markers, preserving Android behavior. Markdown groups the composition in
   its existing history and finalizes after final caret state; cancel adds no undo
   record. External application edits invalidate obsolete rollback state.
6. Shared caret and composition adornment use the actual plain/styled render block,
   its text origin and existing presentation/scroll transformations. Native adapters
   convert host logical coordinates using actual DPI/density. No second text layout.
7. Activate Windows IMM32 via existing native hooks, with balanced context lifetime,
   exact-once result handling, explicit cancellation and candidate/caret positioning.
   Evaluate TSF against this slice and document unsupported text-service capabilities;
   do not claim TSF parity from IMM32 compatibility or add unused COM scaffolding.
8. Android InputConnection captures the offered client identity. Restart, focus loss,
   suspension and detach retire old connections. Implement bounded surrounding and
   extracted queries, native options/actions and cursor updates; use typed privacy
   policy for password inputs. Preserve existing hardware routing for #109.

All mutable operations remain on the existing UI thread. No additional dispatcher,
global manager, polling timer, native text control, dependency or public API removal
is planned. Renderer, accessibility, command and Designer behavior is preserved.
Designer metadata can discover ordinary additive properties; no arbitrary project
code execution or separate Designer text engine is introduced.

## Validation and delivery

Deterministic coverage will exercise all initial editors and a custom client,
composition start/update/commit/finish/cancel, typed rollback/history, Unicode and
length boundaries, reversed ranges, bounded queries and large requests, read-only,
password privacy, callback edits/failures, focus/ancestor removal, modal/popup,
detach/recreation/disposal, stale native sessions and rendered caret transforms.
Use real TestHost and existing platform test projects; no fake editor implementation.

Run restore, serial Debug/Release solution builds and complete regression suites,
API compatibility against this master, package validation and an isolated package
consumer. Update XML documentation, a consumer composition guide, known limitations,
roadmap and appropriate gallery/cross-platform sample. Validate DocFX and archives.
Perform practical native Windows and Android emulator scenarios with exact source
and binary provenance. Record injected/automated, emulator, physical-device and manual
evidence separately. Unavailable required observations remain
`NOT EXECUTED — environment unavailable`, not a passing build-derived assertion.

Return to every issue criterion after implementation. Keep the umbrella open when
the remaining native language/vendor/device acceptance is partial. Push the completed
scope, create a dedicated non-Draft PR, verify required CI, fix failures, perform
final review, merge with the repository's normal strategy, verify/fetch the new
master, then audit #109. No release, version bump, package publication or tag.
