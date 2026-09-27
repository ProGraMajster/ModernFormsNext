# Application screenshots

[Documentation index](README.md) · [Sample applications](samples.md)

## Current Windows captures

The images embedded in the project README and sample guide were captured on **2026-09-27** from
real Windows sample windows built in Debug with .NET SDK `10.0.401`.

Capture source: `e78c28803c5dc5392fc1143f722566c3cfbacfc2`. Its source tree is identical to
`master` at `b574494e75c0a0f0b044623a20b6d5b5d06bacd9`, verified during this refresh.
The images therefore show the sample implementation at that revision. They do not update
previously published release bundles.

| Image | View | Native window size |
| --- | --- | --- |
| [ControlGallery](controlgallery-windows.png) | DataGridView: cell selection and data binding | 1180 × 760 px |
| [Explorer](explorer-windows.png) | Home ribbon and Windows system directory | 1180 × 760 px |
| [Outlaw](outlaw-windows.png) | Initial mail-style view with generated sample messages | 1360 × 850 px |

All three windows used 96 DPI / 100% display scaling. Captures used the Windows `PrintWindow`
API with `PW_RENDERFULLCONTENT` and include the framework title bar. This captures the application
window without unrelated desktop windows. The PNGs were visually inspected for complete frames,
readable content, and unintended desktop content; they were not retouched or generated from mockups.

Explorer displays a real system directory, so folder names and counts can vary between machines.
Outlaw generates its sample message times relative to launch time and uses the local culture.
The images document these views only; they do not establish every control state or DPI/platform
combination.

## Refreshing the images

1. Record the source commit with `git rev-parse HEAD`, and inspect `git status --short`.
2. Restore and build `ModernFormsNext.slnx`; use the commands in the
   [project README](../README.md#build-the-repository).
3. Run each sample using the [sample guide](samples.md). For already built binaries, add
   `--configuration Debug --no-build --no-restore`.
4. At 100% display scaling, select DataGridView in ControlGallery, navigate Explorer to a
   non-personal system directory, and leave Outlaw in its initial mail view. Match the window
   dimensions above where practical.
5. Capture the application window, including its title bar. Exclude unrelated desktop content,
   tooltips, and transient menus. Preserve the rendered pixels without resizing or retouching.
6. Inspect every PNG and its rendered Markdown page. Check labels, clipping, privacy, and image
   links; update the date, revision, and dimensions here if they change.

Keep the existing image names when refreshing these views so README and offline documentation
links remain stable. Keep one-off capture helpers, intermediate images, and build logs out of
tracked documentation.

## Historical images

The following legacy assets are retained at their original paths for existing links. Their source
revision and capture environment have not been established by this refresh, and they are excluded
from the current sample gallery:

- [Explorer on Ubuntu](explorer-ubuntu.png)
- [Explorer on macOS](explorer-osx.png)
- [Legacy decompiler image](modern-decompile.png)

ModernFormsNext does not currently provide supported Linux or macOS application backends.
Use [platform status](platform-specific-features.md) for current support.
