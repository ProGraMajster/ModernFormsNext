# ModernFormsNext Visual Studio Extension

This project builds the Visual Studio extension that registers the ModernFormsNext designer.

The extension is versioned with the repository package version. The current release line is
`1.7.0`.

## Build

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Debug /p:EnableWindowsTargeting=true
```

The build creates the VSIX package at:

```text
ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix
```

## Install into the Experimental Instance

The recommended local install path is the helper script below. It uninstalls any
older copy, installs the current VSIX, and forces Visual Studio to rebuild the
Experimental Instance package registration.

Close the Experimental Instance first, then run:

```powershell
.\ModernFormsNext.VisualStudioExtension\Install-Experimental.ps1
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
and rerun `Install-Experimental.ps1`.

## Manual verification

The experimental instance should show **ModernFormsNext Designer** in **Extensions / Manage Extensions**.

Verify:

- Right-clicking `MainForm.cs` in a ModernFormsNext template project shows **View ModernFormsNext Designer**.
- Right-clicking `Program.cs` does not show **View ModernFormsNext Designer**.
- Right-clicking `MainForm.Designer.cs` does not show **View ModernFormsNext Designer**.
- `MainForm.cs` is marked with `<ModernFormsNextDesigner>true</ModernFormsNextDesigner>` and does not use `<SubType>Form</SubType>`.
- Saving the designer writes `MainForm.mfdesign` and regenerates `MainForm.Designer.cs` without
  modifying `MainForm.cs`.
