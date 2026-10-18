# ADR: Page and navigation architecture

- Status: Runtime implemented by #302; composition, routing and shell remain proposed
- Date: 2026-07-17
- Runtime update: 2026-10-18

## Context

The original proposal started with `Form`, `WindowBase`, `Control`, `NavigationPane`, and
`TabControl`. The runtime slice now adds `Page`, `ContentPage`, `PageHost` and `NavigationPage`;
see [the runtime contract](../../pages-and-navigation.md). Routes, composition containers and
an application shell remain later sequential slices. Windows uses `IWindowImpl`; Android's
experimental Application/Form backend now supports one main Form plus modal descendants and
popups in an externally owned Activity loop. Neutral `SkiaControlSurface` hosting also remains.

## Problem

Add `Page`, `ContentPage`, `NavigationPage`, `TabbedPage`, `FlyoutPage`, routing, and `AppShell`
without equating a page with a native window, duplicating control layout/input, or making shared APIs
depend on Windows handles or Android activities.

## Options considered

1. Make every `Page` a `Form`/native window.
2. Make `Page` a lightweight `Control` root with lifecycle coordinated by a platform-neutral host.
3. Model pages as view-model-only objects that manufacture controls on demand.
4. copy another framework's shell and route implementation.

## Decision

`Page` derives from `Control` and owns page metadata plus an explicit lifecycle state machine.
`ContentPage` hosts one content control. A platform-neutral `PageHost` presents one active page in an
existing window or Skia surface. Appearing/disappearing events are driven by host transitions, not
by native handle creation.

`NavigationPage` owns a stack and serializes push/pop operations on the UI dispatcher. Its runtime
contract defines event order, cancellation, back navigation, and transition ownership. No new
Application or command runtime is introduced. The remaining proposal is: routing is a separate service
that maps normalized route patterns to factories and parameters; it has no renderer dependency.
`TabbedPage` and `FlyoutPage` compose child pages and reuse existing layout/input primitives but do
not subclass `TabControl` or `NavigationPane` if that would expose item/control semantics as page
lifecycle semantics. `AppShell` is the final composition layer over routes, navigation hosts, tabs,
and flyout descriptors.

## Consequences

- The same page tree can run inside a Windows form or Android `SkiaControlSurface`.
- Native windows remain a separate concern and multi-window APIs can be added independently.
- Lifecycle requires precise state and event-order tests, including interrupted async navigation.
- Pages remain controls and use normal resources, data binding, layout, rendering, and input.
- Back reuses the existing raw BrowserBack down/up and scoped command route. Android preserves
  popup priority and dialog/main close fallback; Windows window closing remains separate.
- Deep-link activation is backend/host integration, while route parsing stays shared.

## Rejected alternatives

- A page-per-window model cannot work for normal mobile navigation and would leak platform details.
- View-model-only pages would force a second visual-tree/lifecycle abstraction beside `Control`.
- Directly copying another framework's shell would import assumptions about XAML, handlers, and
  native controls that do not match ModernFormsNext rendering.
