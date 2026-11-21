# Windows custom chrome maximization

For ordinary top-level windows, keep `WS_CAPTION` in the native style when
ModernFormsNext draws the title bar. This makes Windows use the same native
outer maximize bounds as system chrome. The custom handlers for non-client
calculation and painting still own the client area and title bar. The native
system menu stays disabled when using custom chrome, and child and popup windows
keep their existing style policy.

At the initial explicit `Show`, a launcher can supply `SW_HIDE` in
`STARTUPINFO`. If that keeps the newly captioned HWND hidden, the backend
applies the requested show state after Windows consumes the startup hint.
An intervening managed hide or close takes precedence. The standalone
Designer startup test exercises this path by launching with a hidden hint
and requiring a visible top-level HWND before sending its first commands.

In [issue #534](https://github.com/ProGraMajster/ModernFormsNext/issues/534),
the custom client rect matched the monitor work area, while the DWM frame
covered the taskbar. On a Windows 11 desktop with the persistent taskbar, adding
`WS_CAPTION` to the same test form corrected the outer bounds and taskbar hit
ownership. Adding only `WS_SYSMENU` reproduced the overlap. This comparison
identifies the style that selects the correct maximize geometry on the tested
system. The Windows shell's internal decision process was outside the measurement.

`WindowMaximizeGeometryTests` compares the custom HWND to a system-decorated
HWND on every attached monitor. It checks native maximize and restore, confirms
that the custom client fills the work area, and covers the reserved taskbar strip
through the comparison of native outer bounds. Actual Explorer visibility and
hit testing require a separate interactive run.

For an interactive comparison, use identical ordinary Forms with and without
`UseSystemDecorations`. On each monitor, record `GetWindowRect`, the client rect
in screen coordinates, DWM extended frame bounds, and `rcWork`. Verify the
foreground HWND and sample taskbar visibility and hit ownership after the
maximize animation and shell state have settled. With auto-hide enabled, move
the pointer to the taskbar edge and verify that the bar reveals over the
maximized form. Confirm that restoring the form returns it to its normal bounds.
