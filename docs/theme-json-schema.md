# Theme JSON schema reference

ModernFormsNext theme JSON is an untrusted-input format with one supported major schema version.
The current value of `schemaVersion` is `1`. Unknown/duplicate properties and unsupported future or
past versions are rejected. An internal migration hook exists for future framework-owned
migrations, but version 1 registers no migrations.

## Root document

```json
{
  "schemaVersion": 1,
  "id": "product.dark",
  "name": "Product Dark",
  "description": "Optional description",
  "author": "Optional author",
  "baseTheme": "modernformsnext.dark",
  "variant": "Dark",
  "metadata": {},
  "tags": [],
  "colors": {},
  "brushes": {},
  "typography": {},
  "spacing": {},
  "padding": {},
  "sizing": {},
  "corners": {},
  "borderThickness": {},
  "animations": {},
  "resources": {}
}
```

Required properties are `schemaVersion`, `id`, and `name`. All properties are case-sensitive.
`variant` defaults to `Custom`; allowed values are `Light`, `Dark`, `System`, and `Custom`.
`baseTheme` is only an identifier. The serializer never loads another file; ThemeManager resolves
the ID against definitions the application explicitly registered.

IDs, metadata names, token names, and resource names start with an ASCII letter and contain 1–128
ASCII letters, digits, `.`, `_`, or `-`. Values in `metadata` and `tags` are strings.

## Colors

Colors use `#RRGGBB` or `#AARRGGBB`:

```json
"colors": {
  "Background": "#FF202020",
  "Brand.Primary": "#7F7846D8"
}
```

## Brushes

Every brush is an object with an allow-listed `type`. Every type accepts `opacity` in the inclusive
range 0–1 and `transform` as six finite `Matrix3x2` values:

```json
"transform": [ 1, 0, 0, 1, 12, 8 ]
```

### Solid

```json
{
  "type": "solid",
  "color": "#FF7846D8",
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

### Linear gradient

```json
{
  "type": "linearGradient",
  "gradientStops": [
    { "color": "#FF7846D8", "offset": 0 },
    { "color": "#FFE05B9B", "offset": 1 }
  ],
  "spreadMode": "Pad",
  "start": [ 0, 0 ],
  "end": [ 1, 1 ],
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

### Radial gradient

```json
{
  "type": "radialGradient",
  "gradientStops": [
    { "color": "#FFFFFFFF", "offset": 0 },
    { "color": "#00000000", "offset": 1 }
  ],
  "spreadMode": "Reflect",
  "center": [ 0.5, 0.5 ],
  "origin": [ 0.4, 0.4 ],
  "radius": 0.8,
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

### Sweep gradient

```json
{
  "type": "sweepGradient",
  "gradientStops": [
    { "color": "#FFFF0000", "offset": 0 },
    { "color": "#FF0000FF", "offset": 1 }
  ],
  "spreadMode": "Repeat",
  "center": [ 0.5, 0.5 ],
  "startAngle": 0,
  "endAngle": 360,
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

Gradient `spreadMode` values are `Pad`, `Repeat`, and `Reflect`. A gradient needs at least two
stops. Offsets are finite values in 0–1. Points, radii, angles, opacity, and transforms must satisfy
the existing Brush property validation.

### Glass and no fill

```json
{
  "type": "glass",
  "tint": "#1CFFFFFF",
  "secondaryTint": "#0CFFFFFF",
  "highlight": "#26FFFFFF",
  "border": "#41FFFFFF",
  "showHighlight": true,
  "showInnerBorder": true,
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

```json
{
  "type": "none",
  "opacity": 1,
  "transform": [ 1, 0, 0, 1, 0, 0 ]
}
```

No CLR type name, assembly name, or reflection-based type activation is accepted. Exact framework
Brush types are written; unknown subclasses are rejected.

## Typography

```json
"typography": {
  "Body": {
    "fontFamily": "Segoe UI",
    "size": 14,
    "style": "Regular",
    "lineHeight": 1.4,
    "letterSpacing": 0.2
  },
  "Title": {
    "fontFamily": "Segoe UI",
    "size": 24,
    "style": "Bold"
  }
}
```

`size` is positive and finite. Style supports existing `FontStyle` names/flags. `lineHeight` is an
optional positive multiplier; `letterSpacing` is an optional finite logical-pixel value. Those last
two values are preserved even though the global renderer does not yet apply them universally.

## Numeric and animation tokens

`spacing`, `sizing`, `corners`, and `borderThickness` map names to finite non-negative numbers in
logical pixels. First-class `padding` tokens map names to non-negative four-sided values:

```json
"padding": {
  "Control": { "left": 8, "top": 4, "right": 8, "bottom": 4 }
}
```

```json
"animations": {
  "ThemeTransition": {
    "durationMs": 250,
    "easing": "EaseInOut",
    "enabled": true
  }
}
```

Allowed easing values are `Linear`, `EaseIn`, `EaseOut`, and `EaseInOut`. Duration is finite,
non-negative, and at most ten minutes.

## Custom resources

Custom resources use a closed `{ "type", "value" }` union:

| `type` | JSON value / framework value |
| --- | --- |
| `string` | string |
| `boolean` | Boolean |
| `integer` | 32-bit signed integer |
| `number` | finite double |
| `color` | `#RRGGBB` / `#AARRGGBB` |
| `brush` | an allow-listed Brush object |
| `padding` | `{ "left", "top", "right", "bottom" }` integers |
| `typography` | typography object |
| `animation` | animation object |

Example:

```json
"resources": {
  "ProductName": { "type": "string", "value": "Contoso" },
  "CardPadding": {
    "type": "padding",
    "value": { "left": 16, "top": 12, "right": 16, "bottom": 12 }
  }
}
```

A derived theme may override a custom resource only with the same `type`.

## Security defaults

| Limit | Default |
| --- | ---: |
| UTF-8 document size | 1,048,576 bytes |
| JSON nesting depth | 64 |
| combined theme tokens | 4,096 |
| gradient stops per Brush | 64 |
| string length | 512 characters |
| inheritance depth | 16 definitions |
| animation duration | 10 minutes |

`ThemeSecurityLimits` can lower or raise the positive complexity limits for a trusted application,
except the fixed maximum animation duration. Non-finite numeric values, invalid colors/enums/keys,
comments, trailing commas, unknown properties, duplicate properties, malformed JSON, and excessive
limits throw `ThemeSerializationException`. `JsonPath` and the exception message identify the
logical location without disclosing local file-system paths or probing adjacent files.

## APIs and ownership

- `Deserialize(string)` and `Serialize(theme)`;
- stream sync/async operations leave caller-owned streams open;
- `LoadFile`/`SaveFile` and async forms operate only on the explicitly supplied path;
- pretty printing is opt-in for strings/streams and enabled by default for file save;
- dictionary keys are serialized in ordinal order for deterministic output;
- the serializer returns a mutable definition; ThemeManager clones it before validation/apply.
