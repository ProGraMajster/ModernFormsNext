# ADR: Optional feature packages

- Status: Proposed
- Date: 2026-07-17

## Context

The main `ModernFormsNext` package already contains controls, renderers, brushes, data binding, a
document model, Markdown parsing, `DocumentViewer`, and rich-text dependencies. PDF engines,
advanced document editing, charts, diagrams, format codecs, and printing adapters can add large
managed or native dependencies and platform-specific obligations.

## Problem

Choose package boundaries that keep the base framework lightweight while preserving a coherent
public API and allowing providers/renderers to integrate with controls, resources, themes, input,
and platform services.

## Options considered

1. Put every feature and dependency in `ModernFormsNext`.
2. Create one package per public control with no shared contracts.
3. Keep lightweight contracts/core controls in focused modules and place engines/codecs/backends in
   optional packages.
4. Use native platform viewers for all document and chart content.

## Decision

Keep foundational UI concepts in `ModernFormsNext`: dynamic resources, localization contracts and
default JSON provider, page/navigation primitives, lightweight collection controls, SearchBar,
TimePicker, shape geometry, and provider-facing host abstractions that do not require heavy engines.

Create optional modules along these boundaries:

- `ModernFormsNext.Documents`: evolve/extract the existing document model, viewport contracts,
  provider registry, and lightweight `DocumentViewer` host;
- `ModernFormsNext.Documents.Markdown`: existing Markdown parser/renderer and Markdig integration;
- `ModernFormsNext.Documents.RichText`: rich editing and RichTextKit integration;
- `ModernFormsNext.Documents.Pdf`: PDF provider, page rendering, text/link model, cache, and password
  workflow, with platform printing adapters where required;
- `ModernFormsNext.DataVisualization`: chart model, axes, series, interaction, and Skia renderers;
- a later `ModernFormsNext.Diagrams` only after chart primitives prove insufficient.

Backend-specific services remain in `ModernFormsNext.WindowKit.Backend.Windows` and
`ModernFormsNext.WindowKit.Backend.Android`, or in narrowly named adapter packages when an optional
engine would otherwise become a backend dependency. Optional packages depend inward on core; core
never references them.

## Consequences

- Applications pay for heavy codecs and native assets only when selected.
- The existing main-package `Document`, Markdown, `DocumentViewer`, and editor-related APIs create a
  compatibility constraint. Extraction cannot remove or type-forward public APIs without a versioned
  migration plan and package-cycle analysis.
- Provider contracts must be small, platform-neutral, streaming-friendly, cancellation-aware, and
  explicit about resource ownership.
- NuGet package IDs, versioning, licensing, native runtime assets, trimming, and Android ABI size
  become release criteria for each optional engine.

## Rejected alternatives

- One monolithic package would force PDF/chart/editor dependencies on every application.
- One package per control would fragment shared document and visualization models.
- Native viewers would surrender custom rendering, produce platform divergence, and conflict with
  the framework's code-first Skia architecture.
