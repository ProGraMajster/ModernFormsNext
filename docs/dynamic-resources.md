# Dynamic resources

Dynamic resources let a normal public control property follow a value stored at theme,
application, window, ancestor-control, or control scope. They are the shared foundation for
themes, localization, styles, and control defaults; they are not a second data-binding or property
system.

## Define and reference a resource

```csharp
Application.Resources["Button.Primary.Background"] = SKColors.DodgerBlue;

var button = new Button { Text = "Save" };
button.SetResourceReference(
    nameof(Control.BackColor),
    "Button.Primary.Background");
```

Changing the resource invokes the existing CLR property setter. A visual property therefore uses
the control's normal invalidation behavior, while a layout property uses its normal layout behavior.
The resource system does not invalidate the whole application.

```csharp
Application.Resources["Button.Primary.Background"] = SKColors.MediumSeaGreen;
```

Resources may contain any assignable type, not just colors. Brushes, fonts, `Thickness`, styles,
animation configuration, and localized strings can use the same mechanism.

### Observable brushes

Brushes are observable resource values. Once a brush is assigned to a standard brush-valued control
property, changing its color, opacity, transform, gradient geometry, stop collection, or an
individual stop invalidates only controls that still consume it. Replacing the dictionary entry is
not required:

```csharp
var brush = new LinearGradientBrush();
brush.GradientStops.AddRange([
    new GradientStop(System.Drawing.Color.MidnightBlue, 0f),
    new GradientStop(System.Drawing.Color.CornflowerBlue, 1f)
]);

Application.Resources["Card.Background"] = brush;
card.SetResourceReference(nameof(Control.BackgroundBrush), "Card.Background");

brush.GradientStops[0].PaintColor = System.Drawing.Color.Teal;
```

Control subscriptions are weak and reference-counted. Replacing a brush, applying a fallback,
clearing a reference, or disposing a control detaches the obsolete subscription. Other arbitrary
mutable resource objects do not become observable automatically; they still need replacement or a
future type-specific change contract.

## Lookup order and fallback

For a control, lookup proceeds from the most specific scope to the broadest:

1. the control's `Resources`;
2. each ancestor control's `Resources`;
3. the owning window's `Resources`;
4. `Application.Resources`;
5. the read-only, ThemeManager-owned `Application.ThemeResources`.

If an override is removed, the next matching scope becomes effective automatically. If no scope
contains the key, the property returns to the value captured when `SetResourceReference` was first
called. Application resources deliberately override theme defaults, so applying a theme cannot
erase application-owned entries. Reparenting a control refreshes references for that subtree.

```csharp
Application.Resources["Spacing.Page"] = 16;
form.Resources["Spacing.Page"] = 20;
panel.Resources["Spacing.Page"] = 24;

panel.SetResourceReference(nameof(Control.Padding), "Spacing.Page");
```

The example above is illustrative: the resource value must be the exact type accepted by the
target property (`Padding` for `Control.Padding`), not an `int` that happens to look convertible.

## Errors and ownership

An incompatible value found while a reference is created throws `InvalidOperationException` and
does not leave an active reference. If a later runtime replacement has the wrong type, the control
restores its captured fallback and raises `ResourceReferenceFailed`. Property-setter exceptions are
reported through the same event.

```csharp
button.ResourceReferenceFailed += (_, e) =>
    logger.LogError(e.Exception, "Resource {Key} cannot update {Property}",
        e.ResourceKey, e.PropertyName);
```

Directly assigning a referenced property does not remove the reference. Clear it first when manual
assignment should take ownership:

```csharp
button.ClearResourceReference(nameof(Control.BackColor));
button.BackColor = SKColors.CornflowerBlue;
```

References use weak global registrations and are explicitly detached during control disposal.
Removing an undisposed control from the visual tree changes its lookup ancestry but does not dispose
it; application and local resources can still apply while the caller owns the detached control.

## Threading

Dictionary collection operations are protected against corruption, but a mutation immediately
runs affected CLR property setters on the calling thread. Update resources used by live controls on
the UI/dispatcher thread, for example with `Application.RunOnUIThread` when the caller starts on a
worker thread.

## Trimmed and AOT deployments

For statically referenced accessors, reuse a typed descriptor. It delegates to the same binding,
scopes, weak subscriptions, fallback and ordinary CLR setters as the original name-based API:

```csharp
static readonly ResourceProperty<Control, int> CardWidth = new(
    nameof(Control.Width), static control => control.Width,
    static (control, value) => control.Width = value);

card.SetResourceReference(CardWidth, "Card.Width");
```

Base-control descriptors also work on derived controls. Getter/setter delegates are trusted
application code: use the ordinary property so layout/rendering/validation stay authoritative.
Descriptors are reusable, retain no target, and perform no reflection, expression compilation or
dynamic-code generation. Clearing by `CardWidth.Name` restores the captured fallback. A getter,
setter or diagnostic handler that starts a newer update owns the property; an older update cannot
restore its fallback over that newer state. Disposing a control detaches its references.

The original `SetResourceReference(string, object)` signature remains available for JIT callers.
It is marked `RequiresUnreferencedCode`, producing actionable IL2026 guidance during trim analysis.
Callers retaining it must preserve getter/setter metadata for the exact runtime control's public properties
explicitly. A missing property reports that requirement at runtime. Merely using `nameof` does
not preserve a reflected member. See Microsoft's [library trimming guidance](https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming).
The legacy JIT path retains support for a public writable property with a private getter. A
write-only property cannot capture a fallback and is rejected before installing a reference.

The repository's `scripts/validation/DynamicResourcePublish` consumer exercises the complete runtime
assembly, typed inherited accessors, merged replacements, a factory, fallback and closed theme JSON.
Run `pwsh ./scripts/Test-DynamicResourcePublish.ps1` for the sequential JIT, trimmed execution and
negative diagnostic gates; logs and a result manifest are retained under `artifacts/`. CI runs this
same script and retains its diagnostics separately from the normal test result.
Publish it self-contained with `PublishTrimmed=true` and `TrimMode=link`, then run the emitted
executable. Set `ResourceLegacyDiagnostic=true` for a separate negative publish that must report
IL2026 at the name-based call. Keep all trim diagnostics; other framework paths still have independent
reflection requirements. This resource contract does not declare whole-framework full-trim safety
or Windows NativeAOT support. The supported Android deployment remains the existing Release
profiled Mono AOT/partial SDK trimming configuration, qualified separately on native hosts.

## Merged dictionaries

```csharp
var common = new ResourceDictionary { ["Card.Width"] = 240 };
var pageOverrides = new ResourceDictionary { ["Card.Width"] = 280 };
card.Resources.MergedDictionaries.Add(common);
card.Resources.MergedDictionaries.Add(pageOverrides);
// The last merged match wins; a local entry always wins, including an explicit null.
card.Resources["Card.Width"] = 320;
card.Resources.Remove("Card.Width"); // reveals pageOverrides again
```

Within each existing control/window/application/theme scope, lookup searches local entries first,
then merged dictionaries from last to first, recursively. An explicit null stops lookup. Indexer
reads, `TryGetValue` and `ContainsKey` use that effective lookup. `Count`, `Keys`, `Values`, entry
enumeration, `Contains(KeyValuePair)`, `CopyTo` and mutations retain **local-only** semantics.
`Clear()` removes local entries; `MergedDictionaries.Clear()` removes composition. Adding a local
key already present in a merge is a valid override. Removing a merged-only key returns false.
Enumeration never silently flattens another dictionary or transfers ownership.

Composition is an ordered, bounded DAG: no direct duplicates, null children or cycles; at most 256
distinct dictionaries and 64 dictionaries along any root-to-leaf path. Extending an already merged
child checks every affected ancestor before committing. Shared diamond branches count once.
Changes propagate through the existing weak resource hub and each target's current scope. Hidden
effective values do not invoke setters or parent notifications. Merged removal/replacement reveals
the next effective value. Parents are weakly referenced and detached on removal. Topology is committed
before notification, and callbacks run outside locks. Observer failures are reported after other
affected references/keys have been visited. Live composition changes require the owning UI thread.

## Resource factories and ownership

```csharp
card.Resources.SetFactory("Card.Width", static () => 280);
```

`SetFactory` is deliberately eager: it invokes trusted application code once on the existing UI
dispatcher, then stores the result as an ordinary local entry. Reads do not execute code. There is
no retained delegate, automatic recreation, invalidation dependency or hidden retry. An explicit
second `SetFactory` call evaluates again; replacement/removal/clear end that entry's cache lifetime.
Null is a valid result. Creation exceptions propagate without changing the previous entry. A callback
that changes the same dictionary causes the outer registration to fail instead of overwriting its
newer state. Nested factories across dictionaries are limited to 32 calls. An observer exception
after publication follows normal dictionary semantics: the value is already committed.

The application owns and disposes every produced object, including a result created before a
reentrant registration is rejected. The dictionary never disposes values on replacement, clear,
merge removal or collection. Use factories to construct shared UI resources on the UI thread;
do not start asynchronous work or mutate unrelated live controls from a factory.

Factories and descriptors are **code-only**. Theme JSON remains the closed `ThemeDefinition` /
`ThemeResourceValue` schema; its data values can be round-tripped and published through ThemeManager
without serializing delegates, CLR type names or merged runtime graphs. `.mfdesign`, metadata
discovery and preview do not execute factories or arbitrary getter/setter delegates. No new
resource-file format or Designer resource browser is introduced by this feature.

## Current limits

- Application/window/control keys and values remain arbitrary runtime objects. Theme JSON uses the
  stricter typed key and value allow-list documented in [theme JSON schema](theme-json-schema.md).
- Name-based property references require preserved metadata; prefer typed descriptors for trimming.
- Theme inheritance still resolves before atomic publication. Runtime dictionary composition is
  separate from the theme JSON inheritance format; compatible transitions use the shared scheduler.
- In-place updates are currently observed for the framework brush hierarchy only, not for every
  mutable resource object.
- Resource values are applied directly. General-purpose implicit conversion is intentionally not
  performed because it would make theme errors dependent on culture and converter availability.
