# ModernFormsNext Visual Studio Extension

This project builds the Visual Studio extension that registers the ModernFormsNext designer.

The extension version is stored as `ModernFormsNextVisualStudioExtensionVersion` in
`Directory.Build.props`. Coordinated framework releases use the same version; the 1.10.0 VSIX is
therefore versioned 1.10.0. An independent extension-only patch remains possible when it is
deliberate and documented. The same version must be kept in
`ModernFormsNext.VisualStudioExtension.Vsix\source.extension.vsixmanifest` and
the two `InstalledProductRegistration` attributes.

The local VSIX uses `InstallationTarget Version="[17.0,)"` and
`Microsoft.VisualStudio.Component.CoreEditor Version="[17.0,)"`. Visual Studio
2026 evaluates VSIX compatibility through the supported Visual Studio API level,
so the lower bound must remain `17.0` even when the extension is tested in a
Visual Studio 2026 Experimental Instance.

The packaging project validates the generated `.vsix` after every build. The
build fails if the final package contains a stale manifest, a lower bound of
`18.0`, missing VS Package or item-template assets, or a Debug/Release manifest
mismatch when both configurations have been built.

## Build

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Debug /p:EnableWindowsTargeting=true
```

The build creates the VSIX package at:

```text
ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix
```

## Install into the Experimental Instance

The recommended local install path is the helper script below. It removes stale
ModernFormsNext Designer state from the Experimental Instance, installs the
current VSIX, and forces Visual Studio to rebuild package registration. For the
`Exp` hive it removes stale extension folders, Visual Studio cache files, and
the Experimental Instance `privateregistry.bin` file. This is required after
some repeated local installs because Visual Studio can keep a package `CodeBase`
that points at a deleted random extension folder.

The script automatically closes Visual Studio instances that are already running
with `/RootSuffix Exp`. If a normal Visual Studio instance is still open, the
script stops with a list of running processes instead of closing the main IDE
without permission. Close normal Visual Studio manually, then run:

```powershell
.\ModernFormsNext.VisualStudioExtension\Install-Experimental.ps1
```

If the open normal Visual Studio instance is disposable and you want the script
to close it too, run:

```powershell
.\ModernFormsNext.VisualStudioExtension\Install-Experimental.ps1 -ForceCloseVisualStudio
```

The script installs:

```text
ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix
```

You can also run the commands manually:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" /rootSuffix:Exp /uninstall:ModernFormsNext.Designer
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" /rootSuffix:Exp ".\ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix"
```

After installation, launch Visual Studio with:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /RootSuffix Exp /updateconfiguration
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /RootSuffix Exp
```

If the command table looks stale after repeated local VSIX installs, uninstall **ModernFormsNext Designer**
from the Experimental Instance first, close Visual Studio, reinstall the newest VSIX, and run
`devenv.exe /RootSuffix Exp /updateconfiguration` before testing again.

If Visual Studio reports that `ModernFormsDesignerPackage` failed to load and
the ActivityLog points to an older random extension directory such as
`...\Extensions\abc123.tmp\ModernFormsNext.VisualStudioExtension.dll`, the
Experimental Instance still has stale package registration. Close Visual Studio
and rerun `Install-Experimental.ps1`. The script clears the Experimental
Instance private registry before reinstalling so Visual Studio rebuilds the
package registration from the current VSIX.

## Manual verification

The experimental instance should show **ModernFormsNext Designer** in **Extensions / Manage Extensions**.

Verify the full checklist in
[`docs/visual-studio-designer-host.md`](../docs/visual-studio-designer-host.md). At minimum:

- Selecting `MainForm.cs` exposes standard **View Designer**, and Shift+F7 opens the embedded
  ModernFormsNext Designer pane.
- Right-clicking `MainForm.cs` in a ModernFormsNext template project also shows
  **View ModernFormsNext Designer**.
- Right-clicking `Program.cs` does not show **View ModernFormsNext Designer**.
- Right-clicking `MainForm.Designer.cs` does not show **View ModernFormsNext Designer**.
- `MainForm.cs` is marked with `<ModernFormsNextDesigner>true</ModernFormsNextDesigner>` and does not use `<SubType>Form</SubType>`.
- **Add** > **New Item** lists **ModernFormsNext Form** for C# projects and creates `.cs`,
  `.Designer.cs`, and `.mfdesign` files together.
- `.Designer.cs` and `.mfdesign` appear nested under the primary `.cs` file without hand-editing
  the project file.
- **Add** > **New Item** lists **ModernFormsNext UserControl** and creates the same three-file
  structure with `rootKind: userControl`.
- Right-clicking a partial class derived from `ModernFormsNext.UserControl` shows
  **View ModernFormsNext Designer**.
- A project UserControl appears under **My Project** in the Toolbox and remains one atomic component
  when placed on a parent Form/UserControl.
- Saving the designer writes `MainForm.mfdesign` and regenerates `MainForm.Designer.cs` without
  modifying `MainForm.cs`.
