# ModernFormsNext cross-platform sample

This is one multi-target ModernFormsNext application project. `App` and `MainPage` are compiled
unchanged for Windows and Android. Platform code is isolated under `Platforms/Windows` and
`Platforms/Android`.

The Android host is an explicit transition slice: a single activity supplies an Android
`SKCanvasView`, while `SkiaControlSurface` renders and routes input through the same real
ModernFormsNext `Control` tree used by the Windows window. It is not yet a general Android
`Form`/window backend.

Use the repository scripts documented in `docs/cross-platform-sample.md` to run either target.
