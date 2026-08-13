# Designer animation and interaction-effect definitions

ModernFormsNext Designer edits animation configuration as detached document data. Opening a
Property Grid editor does not construct an `InteractionEffect`, load the application assembly,
invoke a custom constructor, start the animation scheduler, or preview executable project code.
Generated application code remains the only place where configured effects are instantiated.

## Property Grid surface

The design root and ordinary controls expose three separate concepts:

- **Interaction Effects** is an ordered collection. The editor can add built-in `RippleEffect`
  and `PressScaleEffect` entries, keep duplicates when runtime permits them, remove entries, and
  move entries up or down. The selected entry displays its supported properties as detached
  `Property=Value` fields.
- **Layout Transition** maps to the public `Control.LayoutTransition` API. It exposes `Enabled`,
  `DurationMilliseconds`, and a built-in easing identifier. Reset removes the optional value.
- **Visual State Transitions** maps directional `VisualState` pairs to the existing
  `Control.StyleTransitions` collection. Each line identifies `From`, `To`, duration, and easing.

The editors keep private working copies until **OK**. **Cancel** leaves the document unchanged.
There is no independent runtime animation model in Designer and these definitions never appear as
control children in Document Outline. Existing JSON-based control copy/paste deep-copies their
structured values.

## Stable `.mfdesign` representation

Animation configuration uses the existing `DesignPropertyValue` structured-object shape. No
document-format version bump is required, and documents that omit these optional properties retain
their historical behavior.

Interaction effects are stored in `InteractionEffects` with `Count` plus ordered `ItemN` values.
Each item carries a namespace-qualified, assembly-independent runtime type name and only explicitly
changed properties. Transition collections use the same deterministic `Count`/`ItemN` convention.
`LayoutTransition` is one structured value. Default effect properties are omitted so resetting a
field does not create unnecessary generated assignments.

`TimeSpan` properties are stored as finite, non-negative milliseconds. This matches the existing
effect document convention and is displayed as `DurationMilliseconds`. The generator emits
`TimeSpan.FromMilliseconds(...)` and the reverse parser recognizes that exact supported shape.

Runtime easing values remain `Func<float, float>` delegates. `.mfdesign` never serializes a
delegate. It stores a stable identifier such as `Linear`, `EaseOut`, or `CubicOut`, which generation
maps to `ModernFormsNext.Animations.Easings.<identifier>`. An unknown identifier produces a
controlled diagnostic and is not executed or guessed.

## Built-in and project-owned effects

Built-in descriptors are defined once in the neutral Designing layer and are shared by the editor,
generator, and conservative reverse parser. Project effects are opt-in source metadata:

```csharp
[DesignableAnimationDefinition("Glow")]
public sealed class GlowEffect : InteractionEffect
{
    [DesignableAnimationProperty(
        DesignableAnimationPropertyKind.Number,
        DefaultValue = "0.5",
        Minimum = 0,
        Maximum = 1)]
    public float Opacity { get; set; } = 0.5f;
}
```

Discovery parses project-owned `.cs` files with Roslyn. It requires an explicit marker, a public,
non-abstract, non-generic, top-level type and an accessible parameterless constructor. Only public
settable properties with explicit designer metadata are exposed. Discovery excludes build and
repository artifact directories. It performs no `Type.GetType`, reflection, assembly loading, or
constructor invocation. C# aliases and project-owned base classes are resolved from syntax; marked
properties declared on a project base class are inherited by an opted-in concrete definition.

Supported detached custom property shapes are Boolean, Int32, finite number, TimeSpan, known
easing, explicitly described enum, ARGB color, and string. Arbitrary nested CLR objects, delegates,
services, Brushes, and factory expressions are deliberately not deserialized. A missing or renamed
custom effect type stays in `.mfdesign` as an unavailable entry that can be kept, reordered, or
removed; generation reports it and skips unsafe code rather than destroying the definition.

`AnimationDefinition` subclasses can carry the same source metadata, but remain code-first because
the current runtime has no general control-level definition collection or activation contract.
Designer therefore does not offer them in the Interaction Effects list and does not invent a
parallel attachment API.

## Generation and reverse synchronization

Generation preserves interaction-effect order and emits the actual runtime APIs:

```csharp
button1.InteractionEffects.Add(new ModernFormsNext.Animations.PressScaleEffect {
    PressedScale = 0.96f,
    Easing = ModernFormsNext.Animations.Easings.EaseOut
});
```

Layout and visual-state transitions similarly emit `LayoutTransition` and
`StyleTransitions.Add(...)`. The reverse parser supports the complete subset emitted by the
generator. It does not evaluate factories, locals, arbitrary method calls, or user expressions;
unsupported syntax produces diagnostics.

The Visual Studio host passes the same source-discovered descriptors into save/generation and
reverse import. The existing Designer, Designing, and CodeGeneration project references are already
included in the VSIX; no application assembly is added to the extension process.

## Current limitations

- Custom delegate easing remains code-first.
- General custom `AnimationDefinition` activation remains code-first.
- There is no automatic design-surface effect preview; this avoids interfering with selection,
  drag, resize, and custom-code isolation.
- Designer-wide undo/redo does not exist yet. Editors use atomic OK/Cancel semantics so a future
  transaction service can wrap one committed definition change.
- Android runtime animation integration and device validation remain part of issue #29.

## Manual Visual Studio smoke test

For a host-level check, open a Form `.mfdesign`, select a Button, add `PressScaleEffect` and
`RippleEffect`, change their order and easing, configure a 200 ms Layout Transition and one
Normal-to-Hover Visual State Transition, then save. Inspect the sibling `.Designer.cs`, close and
reopen the designer, and run the application. Confirm the order and values survive, the generated
file alone changes, and runtime behavior begins only after the application starts. A project-owned
effect can be checked with the explicit attributes shown above; its throwing constructor must not
run while the designer or editor is open.

See [Designer architecture](designer-architecture.md), [Animations](animations.md), and
[Composable animations](composable-animations.md).
