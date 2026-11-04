# Inherited Form and UserControl designs

The Windows Designer opens partial classes derived from project-owned custom `Form` and
`UserControl` classes. It reads source symbols and `.mfdesign` companions into the existing
design tree. It never constructs a project type, calls its getters, runs attribute constructors
or loads its assembly for execution. Abstract bases can supply design data; the output reports
that the base is abstract. Runtime constructors and inheritance semantics are unchanged.

## Author the base and derived classes

Keep each base's source and companion together, for example `BaseForm.cs` and
`BaseForm.mfdesign`. In the base design, set a container's **Member visibility** to `Protected`
if derived classes should edit supported values or add local children to it. Private controls
remain visible and locked in derived designs.

```csharp
public partial class BaseForm : Form
{
    public BaseForm() => InitializeComponent();
}

public partial class CustomerForm : BaseForm
{
    public CustomerForm() => InitializeComponent();
}
```

`BaseForm.Designer.cs` declares base controls. `CustomerForm.Designer.cs` declares only local
controls and writes derived-owned property, bounds and event assignments. Each constructor
calls its own initialization method; the derived method never constructs or adds a base control
again. The same pattern applies to `UserControl` roots.

The Designer resolves nongeneric top-level bases with an accessible parameterless constructor.
Every custom level needs a project-owned companion with matching namespace/class and root kind.
A same-source companion is preferred; a bounded, unambiguous project scan is the fallback.
Referenced types without a local companion, generic/nested bases, incompatible kinds, sealed
bases, missing constructors and stale base identities produce diagnostics. Correct the source
or companion and reopen the document. Generation is blocked while the active session has an
unresolved inheritance error; authored patches remain available for recovery.

## Editing and ownership

The outline labels inherited and locked members. Private, local-only and unresolved members
remain locked. Accessible compatible inherited fields/properties permit supported scalar, style,
geometry and event editors. Inherited custom root properties and events use the existing inert
metadata catalog. Base subscriptions belong to their declaring constructor; derived bindings
add only the derived handler.

Inherited identity, visibility, parent, order and collections belong to the declaring design.
Rename, copy, delete and reparent cannot alter them. Edit collection contents, table placement/
spans and implicit SplitContainer panels in the declaring design. Local children can be added
to accessible inherited containers and use ordinary undo/redo. Inherited members keep their
relative order: ordinary containers place locals at the visual front; flow, table and tab
containers append local content after inherited content.

Root resize follows existing Anchor/layout rules. Automatic base geometry changes are not
saved as individual private-control overrides. Explicit inherited viewport size is emitted before
explicit child bounds. Resetting an inherited property restores its base value. Base source or
companion changes are resolved on reopen; an open document retains its admitted base snapshot.
Embedded caches likewise belong to the discovery snapshot; Refresh Toolbox creates a new cache.

## Persistence and reverse synchronization

The additive optional `baseTypeName` and `inheritedSize` fields identify the source base and
explicit root size override. Inherited node patches use `inheritedFrom` and optional
`inheritedBounds`. Unchanged inherited controls/values are omitted. Opening composes the complete
tree again without modifying base files. Ordinary documents retain their existing JSON shape.
Ordinary documents continue to use format 1. Saving a custom base writes format 2, which older
format-1 readers reject before materializing the tree. Downgraded inheritance metadata is rejected.
An older derived document without inheritance metadata is migrated only after its base resolves;
its existing root values remain explicit derived overrides.

Generated `MFN-Base`, `MFN-Viewport` and `MFN-Inherited` comments contain bounded identity data
for conservative reverse synchronization. Retain them when editing generated files. Source
name and geometry validation also applies to inherited patches. Programmatic callers must
preserve ownership: serialization and generation reject structural changes rather than silently
discarding them. Older Designer versions cannot open inherited documents.

## Resource admission

Companions use the canonical bounded serializer before text is materialized and must remain
inside the host's local project root without reparse points. Inheritance admits at most 16 custom
levels, 2,048 combined controls and existing graph-depth/value limits. Fallback scanning admits
64 documents, 8,192 filesystem entries and 16 MiB per scan. Source metadata shares event analysis
admission: 512 files, 512 Ki UTF-16 characters per file, 8 Mi project characters, syntax-depth/
expression limits and bounded reference images. Metadata discovery runs no MSBuild targets.
Source fallback admits 4,096 declarations and 64 discovery rounds. Catalog inspection admits
32,768 types, 32,768 namespaces per reference, 64 base levels and 2,048 members per control.
Namespace traversal is iterative. These are independent safety ceilings, not frame-time promises.
Enum lists admit at most 2,048 members; event signatures admit at most 32 parameters, 32 generic
arguments per named type and 32 nested type levels. More complex signatures stay unavailable.

Custom UserControl preview retains at most eight sources, four layouts per source, sixteen
layouts overall and 4 Mi UTF-16 source characters. Each projection admits at most 512 controls,
8,192 logical pixels per viewport axis and 32 Mi logical viewport pixels. Excess data uses a
diagnostic placeholder. Existing [table and bitmap policies](designer-preview-resources.md)
apply independently. These Designer limits do not restrict runtime control counts or layout.
