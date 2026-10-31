# Designer preview resources

`.mfdesign` is data. Opening or painting it must not allocate a runtime table directly from
untrusted strip counts, placements or spans. The Designer preflights table data before runtime
construction, property setters, child admission, measurement, layout and grid painting.

`DesignerPreviewResourcePolicy` belongs to the host/session, outside the document format.
The default ordinary budget is 256 strips per axis and 65,536 combined conservative cells.
In Designer Settings, change **Table preview strips per axis** and **Table preview cell budget**
for a trusted project. Zero disables that ordinary limit. Settings affect the current session;
they are not saved into `.mfdesign`, generated C# or the document undo history. Hosts can persist
them through their normal options storage or configure them before constructing the shell:

```csharp
var options = new ModernFormsDesignerOptions
{
    PreviewResourcePolicy = new DesignerPreviewResourcePolicy
    {
        MaximumTableStrips = 1024,
        MaximumTableCells = 0
    }
};
var designer = new ModernFormsDesignerShell(options);
```

For a running host, assign an immutable policy to `designer.Session.PreviewResourcePolicy` on
the UI thread. The session raises `SettingsChanged`; painting, layout, hit testing, resize and
embedded-document preview use that same policy. Cached embedded projections are invalidated
when the policy changes.

Independent safety ceilings cannot be disabled: 4,096 conservative strips per axis, 262,144
combined conservative cells, and 16,777,216 estimated placement-work units per projection.
The estimate includes authored positions, summed spans, area-based AddColumns estimates,
collision/fragmentation growth, partial placements and repeated column-fitting attempts.
It includes hidden children and both axes conservatively. Nested and sibling tables share the
projection budget. An over-budget table can therefore be deferred even when its eventual visible
portion is small. This is a bounded admission estimate, not a promise of a particular frame time.
The existing graph, node, depth, value, text and device-bitmap limits remain separate.

Malformed numeric kinds/ranges, unrepresentable span endpoints, invalid fixed-table placement
and undeclared grow-style selections produce diagnostics before runtime setters run. Arithmetic
is promoted and checked. Preview accepts canonical table properties; indirect LayoutSettings
property paths are unavailable in preview. No application type or converter is executed to
obtain a layout.

## Lazy preview and runtime compatibility

The former preliminary Designer table projection allocated RowCount/ColumnCount-sized arrays
before the production adapter ran. That redundant projection is removed. The initial pass now
only visits authored nodes. Within budget, Flow/Table geometry comes from the existing inert
production-tree adapter and the canonical runtime engines.

Above budget, the Designer uses a lazy diagnostic placeholder. It creates no runtime table,
strip arrays, reservation grid, per-cell placeholders or full-grid paint loops. Unavailable
descendants are excluded from surface hit/drop targets; their authored data stays in the document
and outline. The output explains the deferred preview. The Designer can retain, serialize and
generate a legal large table without expanding its full dimensions.

A sparse replacement layout was considered. Producing exact TableLayout geometry from only
visible/nonempty strips would require changing its production sizing, spanning, collision and
AutoSize algorithms or implementing another layout engine in Designer. This change uses lazy
deferral instead. It does not claim exact visible-cell previews for tables above the safety
ceilings.

Runtime `TableLayoutPanel`, its setters, layout semantics and support for very large declared
RowCount/ColumnCount are unchanged by this policy. A trusted application retains responsibility
for the resources consumed by actually laying out such a runtime table. Preview limits do not
change document values or add limits to generated runtime applications.
