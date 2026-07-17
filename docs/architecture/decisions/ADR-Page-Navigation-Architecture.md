# ADR: Page and navigation architecture

- Status: Proposed
- Date: 2026-07-17

## Context

ModernFormsNext currently has `Form`, `WindowBase`, `Control`, `NavigationPane`, and `TabControl`, but
no page lifecycle, navigation stack, route model, or application shell. Windows uses a full
`IWindowImpl`; Android currently hosts one shared `Control` tree through `SkiaControlSurface` and
does not implement general `Application.Run(Form)` or multi-window behavior.

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

`NavigationPage` owns a stack and serializes push/pop operations on the UI dispatcher. It defines
event order, cancellation, back navigation, and transition ownership. Routing is a separate service
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
- Back handling needs a platform-neutral WindowKit contract with Windows close/key mapping and
  Android activity back integration.
- Deep-link activation is backend/host integration, while route parsing stays shared.

## Rejected alternatives

- A page-per-window model cannot work for normal mobile navigation and would leak platform details.
- View-model-only pages would force a second visual-tree/lifecycle abstraction beside `Control`.
- Directly copying another framework's shell would import assumptions about XAML, handlers, and
  native controls that do not match ModernFormsNext rendering.
