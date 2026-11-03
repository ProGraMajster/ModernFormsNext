# Designer project assets

Use **Image** in the Property Grid to browse project assets, import a file, or
assign an image to a PictureBox, Button, CheckBox, Label, LinkLabel, RadioButton,
or FormTitleBar. The browser lists files in the two asset folders. It previews
raster images and icons. Fonts and other files are listed and can be imported
for use by application code.

Enable the asset folders in your SDK-style project when using the
ModernFormsNext package:

```xml
<PropertyGroup>
  <ModernFormsNextUseProjectAssets>true</ModernFormsNextUseProjectAssets>
</PropertyGroup>
```

| Folder | Build behavior |
| --- | --- |
| `Assets/Embedded/` | Embed each file with its exact project-relative path as the manifest resource name. |
| `Assets/Content/` | Copy each file to the same relative location in the build and publish output. |

Projects using a framework project reference can import the framework's
`ModernFormsNext.targets` explicitly or declare the same SDK items themselves.
Use forward slashes in identifiers and keep their case consistent. The Designer
accepts letters A–Z, digits, spaces, periods, underscores and hyphens within
portable path segments. The build uses literal manifest names, including any
culture suffix in the filename. Applications can use their own culture fallback
policy to select among these IDs.

For example, importing a logo with **Embed imported asset** selected creates
`Assets/Embedded/logo.png`. The same ID is stored in `.mfdesign`, emitted in C#,
and used at runtime. The declaring form's assembly identifies the embedded asset
even when the form is inherited by a project in another assembly.

```csharp
using ModernFormsNext.Drawing;

ProjectAssets.ApplyImage(picture, "Assets/Embedded/logo.png", typeof(MainForm).Assembly);
```

For other file types, use `ProjectAssets.OpenRead(id, typeof(MainForm).Assembly)`
and dispose the returned stream. For example, an application can read an
imported font with `SKTypeface.FromStream` and use it in `ControlStyle.Font`.
The application owns that typeface and should keep it alive while the style uses
it. Copy-output files are relative to `AppContext.BaseDirectory`. Use embedded
assets on hosts without a loose-file output directory.

Move the project and its asset folders together to preserve references. If you
rename an asset, select its new path in the Property Grid. The Designer reports
missing files and unsupported images and leaves the authored reference intact.
Imports never overwrite a file at the destination. The Designer obtains the root
from the host's project context or, for a saved standalone document, the nearest
unambiguous SDK project directory found within its search budget.

The preview reads local assets from the project folders and skips directory
links. Each image may use up to 8 MiB of encoded bytes, 4096 pixels on either
axis and 4,194,304 decoded pixels. Animated files preview their first frame.
Each surface frame can admit up to eight different image references. The
renderer can reuse a decoded bitmap from the previous frame after checking the
opened file's size and modification time. The browser limits the depth and
number of entries it scans.

Existing `ImageLocation` values are retained in the document and used by the
generated application. The Designer does not invoke this file and URL loader
while painting. To preview an existing image, import or select a project asset
through the Image editor.

Markdown and document controls can render text in the Designer. Their ordinary
image loaders are suppressed there, so document references display placeholders.
