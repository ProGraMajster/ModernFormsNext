# Themes

ThemeManager is the application-wide, code-first theme service. It extends the existing dynamic
resource, Brush, `ControlStyle`, static `Theme`, dispatcher, and animation scheduler systems. It
does not introduce XAML or a second control/property model.

> [!NOTE]
> This guide documents the ModernFormsNext 1.10.0 API. Theme transitions remain opt-in and Android
> integration remains experimental.

## Apply built-in themes

`ThemeManager.Current` has application lifetime. Applying on the UI thread is synchronous. A
background caller can use `ApplyAsync`; validation can occur on that caller, while the commit and
events are dispatched to the UI thread.

Theme transitions are opt-in. `Apply(theme)`, `ApplyAsync(theme)`, and a new `ThemeApplyOptions`
commit and repaint immediately without starting the scheduler. Set
`ThemeTransitionOptions.Enabled = true` explicitly to animate compatible values. The built-in
`ThemeTransition` animation token describes reusable motion settings; it does not activate a
transition by itself.

```csharp
ThemeApplyResult result = ThemeManager.Current.Apply(
    BuiltInThemes.Dark,
    new ThemeApplyOptions
    {
        Transition = new ThemeTransitionOptions
        {
            Enabled = true,
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = ThemeEasing.EaseInOut,
            RespectReducedMotion = true
        }
    });

if (!result.Success)
{
    foreach (ThemeDiagnostic diagnostic in result.Diagnostics)
        Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
}
```

The built-ins are `ModernFormsNext Light` and `ModernFormsNext Dark`. Each property returns an
isolated authoring copy. Both inherit shared typography, metrics, and motion tokens from
`modernformsnext.base`, contain semantic colors, and demonstrate solid and gradient brushes.

The old API remains valid:

```csharp
Theme.SetBuiltInTheme(BuiltInTheme.Dark);
```

It performs an immediate ThemeManager apply and updates the legacy static Theme projection.

## Define a theme in code

```csharp
var theme = new ThemeDefinition("product.dark", "Product Dark")
{
    Description = "Dark product palette",
    Author = "Product team",
    BaseTheme = BuiltInThemes.DarkThemeId,
    Variant = ThemeVariant.Dark
};

theme.Set(ThemeTokens.Colors.Primary, Color.FromArgb(112, 70, 216));
theme.Spacing["Page"] = 24;
theme.Padding["Card"] = new Padding(16);
theme.Corners["Card"] = 10;
theme.Resources["ProductName"] = ThemeResourceValue.FromString("Contoso");

var cardBrush = new LinearGradientBrush();
cardBrush.GradientStops.AddRange([
    new GradientStop(Color.MediumPurple, 0f),
    new GradientStop(Color.DeepPink, 1f)
]);
theme.Brushes["ProductCard"] = cardBrush;

ThemeManager.Current.Apply(theme);
```

Standard color tokens include Background, Surface, SurfaceVariant, TextPrimary, TextSecondary,
TextDisabled, Border, Divider, Primary, PrimaryHover, PrimaryPressed, PrimaryText, Secondary,
Accent, Success, Warning, Error, Info, Focus, and Selection. Applications can create additional
typed identifiers:

```csharp
var chartPositive = new ThemeToken<Color>(ThemeTokenCategory.Color, "Chart.Positive");
theme.Set(chartPositive, Color.SeaGreen);
```

Typography roles include Body, Caption, Heading, Title, Button, and Input. Font family, size, and
style map to the existing renderer. Line height and letter spacing round-trip as tokens but are not
globally applied by the base renderer; only a control with explicit support should consume them.

Spacing, four-sided padding, sizing/control heights/icon sizes, corners, border thickness,
animation settings, and a closed set of custom resource values are supported. Shadows are not yet
modeled because there is no complete shared shadow-rendering contract.

## Inheritance

Register a base before applying a derived definition:

```csharp
ThemeManager.Current.Register(productBase);

var seasonal = new ThemeDefinition("product.winter", "Product Winter")
{
    BaseTheme = productBase.Id,
    Variant = ThemeVariant.Custom
};
seasonal.Colors[ThemeTokens.Colors.Accent.Name] = Color.LightSkyBlue;

ThemeApplyResult result = ThemeManager.Current.Apply(seasonal);
```

One base is allowed. Resolution is root-to-leaf, and a leaf can override selected values. Missing
bases, cycles, excessive depth, invalid values, and incompatible custom-resource kinds fail before
commit. Custom tokens inherit exactly like standard ones. Registered definitions and bases are
copied and never mutated by resolution.

## Immutable snapshot and typed lookup

`ActiveTheme` returns a mutable copy of the authored definition. `ActiveSnapshot` is the validated,
resolved target:

```csharp
ThemeResolvedSnapshot snapshot = ThemeManager.Current.ActiveSnapshot!;
Color primary = snapshot.Get(ThemeTokens.Colors.Primary);

var productCard = new ThemeToken<ModernFormsNext.Drawing.Brush>(
    ThemeTokenCategory.Brush,
    "ProductCard");

if (snapshot.TryGet(productCard, out ModernFormsNext.Drawing.Brush? brush))
{
    // This is an isolated clone, not the manager's applied working brush.
}
```

Brushes are cloned at authoring-copy, registration, resolution, snapshot-read, and publication
boundaries. Applied resource brushes remain mutable so their existing `Changed` notification keeps
render invalidation working. During a transition, one working brush is changed in place; no brush
is allocated per frame.

## Dynamic resources and precedence

Theme resource keys have the stable form `Theme.{Category}.{Name}`:

```csharp
card.SetResourceReference(
    nameof(Control.BackgroundBrush),
    ThemeResourceKeys.Create(ThemeTokenCategory.Brush, "ProductCard"));
```

Lookup order is control, parents from nearest to farthest, window, `Application.Resources`, then
the manager-owned `Application.ThemeResources`. The captured CLR property value is the final
fallback. An application resource with the same key therefore overrides a theme default. Theme
application never clears unrelated `Application.Resources` entries.

Dynamic references update through the existing CLR setter without recreating controls. Static
assignment remains static and does not follow a later theme change.

## Apply, rollback, and event ordering

The target definition is validated, inherited, and materialized before UI state changes. Commit
replaces the dedicated resource snapshot and legacy Theme projection together, then publishes
deferred keyed changes. If a commit observer rejects the change, ThemeManager restores the previous
snapshot/resources/legacy values and reports failure.

Event order is:

1. `ThemeChanging` before mutation; set `Cancel` to stop the request;
2. resource commit and dynamic-reference refresh;
3. optional transition start;
4. `ThemeChanged`, meaning the target was committed;
5. `ThemeTransitionCompleted` when animation completes, is canceled, or faults.

Before the repaint request is flushed, the legacy style projection refreshes every open form,
the active popup, and each complete nested control tree on the UI thread. The current Normal,
Hover, Pressed, Focused, Disabled, and control-specific interaction state is preserved; renderers
resolve that state again against the committed style values. Dynamic-resource setters and control
theme handlers can issue multiple visual invalidations, but ThemeManager coalesces them into one
platform repaint request per affected window for each commit or animation tick. Generic theme
refresh does not force layout; controls whose cached measurements depend on theme typography or
metrics may request their existing targeted layout path from `OnThemeChanged`. Animation ticks
bypass those layout/cache hooks and perform visual-only subtree invalidation because layout tokens
have already committed before the transition starts.

`ThemeApplyResult.Transition` exposes state, explicit cancellation, and a completion task. Explicit
cancel snaps to the committed target. A rapid newer apply replaces the old transition from its
current interpolated values. A stale handle cannot cancel the newer transition. `ReplacementMode`
defaults to `Replace`; `IgnoreNew` instead cancels the new apply request and leaves the already
running transition untouched.

## Animation and motion policy

Only the shared `AnimationScheduler` is used. Compatible changed values are:

- colors and allow-listed custom numeric resources;
- solid brush color, opacity, and transform;
- compatible linear, radial, and sweep gradients, including geometry, opacity, transform, stop
  colors, and stop offsets.

An incompatible Brush type or gradient stop count switches immediately. Glass and no-fill brushes
also switch immediately. Spacing, padding, sizing, corners, borders, typography, and other layout
values switch at commit to avoid a layout pass on every frame.

Platform reduced-motion can suppress transition creation. The scheduler's `AnimationsEnabled`,
`ReducedMotion`, and duration policy is authoritative, including for active work. Background/no-host
lifecycle pauses exclude elapsed background time.

## Load and save JSON

```csharp
var serializer = new ThemeJsonSerializer();
ThemeDefinition loaded = await serializer.LoadFileAsync(selectedPath, cancellationToken);
ThemeApplyResult result = await ThemeManager.Current.ApplyAsync(
    loaded,
    cancellationToken: cancellationToken);

string preview = serializer.Serialize(loaded, indented: true);
```

Only the explicitly supplied string, stream, or file is read. A `baseTheme` string never causes
adjacent file access. See [theme JSON schema](theme-json-schema.md) for the full allow-list and
security limits.

## Diagnostics

```csharp
ThemeManagerDiagnostics diagnostics = ThemeManager.Current.GetDiagnostics();
Console.WriteLine($"{diagnostics.ActiveThemeId}: {diagnostics.TokenCounts.Total} tokens");
Console.WriteLine($"Transition: {diagnostics.TransitionState}");
```

The read-only snapshot includes active ID/name/variant/schema, base chain, token counts, last apply
duration, transition state, latest validation diagnostics, safe last-failure details, and
successful/canceled/failed counters. Scheduler diagnostics remain available separately through
`AnimationScheduler.Default.GetDiagnostics()`.

## Designer and platforms

Designer mode uses the explicit System fallback, applies immediately, hides complex definition
collections from the generic property grid, and starts no platform queries, watcher, worker, or
animation. JSON remains the stable interchange format. File hot reload is not implemented.

Windows reads the current application light/dark and client-animation preferences on each relevant
apply. It does not yet listen for live OS preference changes; reapply a System theme when the host
observes such a change.

Android support is experimental. The shared model, JSON stream APIs, dynamic resources,
transitions, and background-time exclusion build for Android. No Android platform theme provider
is registered yet, so System uses `ThemeApplyOptions.SystemFallbackVariant`. Emulator/device
startup, visual transitions, and storage integration remain manual validation items.

## ControlGallery checks

Open **ThemeManager** in ControlGallery. The page provides Light/Dark immediate and animated
switches, rapid replacement, cancel, animation/reduced-motion policy toggles, inherited custom JSON,
malformed JSON, an invalid Brush discriminator, missing-base and cycle errors, semantic/metric
values, ThemeManager counters, and scheduler diagnostics. Leaving the page cancels its transition
and restores the scheduler policies that were active before the page was opened.

### Manual validation checklist

- [ ] Light → Dark, immediate;
- [ ] Light → Dark, animated;
- [ ] three rapid switches replace cleanly;
- [ ] explicit cancel snaps to the committed target;
- [ ] disabling animations during a transition completes scheduler work;
- [ ] enabling reduced motion during a transition completes scheduler work;
- [ ] leaving and re-entering the page restores its policy subscriptions;
- [ ] minimize and restore while transitioning;
- [ ] resize while solid and gradient resources are active;
- [ ] load the valid inherited JSON theme;
- [ ] reject malformed JSON with a safe path;
- [ ] reject a missing base and an inheritance cycle;
- [ ] reject an invalid Brush discriminator;
- [ ] inspect linear/radial gradient interpolation (sweep remains deterministically tested);
- [ ] confirm active scheduler work returns to zero and the tick source stops;
- [ ] verify Android background/foreground on an emulator or device;
- [ ] verify the Designer starts no runtime theme service.

Headless tests cover the corresponding state transitions and lifecycle contracts. The checklist is
kept for rendering, window-lifecycle, emulator/device, and interactive Designer verification.

## Migration from the static Theme API

- Existing `Theme` properties and `Theme.SetBuiltInTheme` remain source-compatible.
- Prefer semantic `ThemeTokens` and dynamic resource references for new reusable controls.
- Use `Application.Resources` for application/window/control overrides; theme resources are defaults.
- Do not retain or mutate an authoring Brush expecting it to change an applied theme; retrieve or
  dynamically bind the applied resource instead.
- Treat `ThemeChanged` as commit notification, not transition completion.
- Theme JSON is strict: unknown properties and unsupported polymorphic types that may previously
  have been ignored by application-specific loaders are rejected.

## Known limitations

- no automatic OS-theme-change notification;
- no file hot reload;
- no animated layout tokens, GlassBrush, NoBrush, or incompatible gradients;
- no shared shadow token/rendering contract;
- line height and letter spacing are not globally honored;
- Android has no system-theme provider and remains experimental;
- physical-device Android and interactive Designer checks cannot be replaced by headless tests.
