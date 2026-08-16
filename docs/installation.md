# Installing ModernFormsNext

ModernFormsNext is distributed as NuGet packages, project templates, and a Visual Studio
designer extension. The framework remains code-first, but the designer can edit the companion
`.mfdesign` document and regenerate the matching `.Designer.cs` file.

The commands below target the coordinated 1.10.0 release. They become publicly available after the
NuGet packages and GitHub release assets are published.

## Requirements

- .NET SDK `10.0.201` or a compatible .NET 10 SDK feature band.
- Windows for the current desktop backend and Visual Studio designer experience.
- Visual Studio 2022/2026 for the VSIX designer extension.

## Install the Framework Package

For an existing application, reference the framework package:

```powershell
dotnet add package ModernFormsNext --version 1.10.0
```

The package provides the runtime controls, forms, rendering, layout, input, dialogs, and
Windows backend integration used by ModernFormsNext applications.

## Install the Project Template

Install the template package:

```powershell
dotnet new install ModernFormsNext.Templates::1.10.0
```

Create a new app:

```powershell
dotnet new mfn-app -n MyApp
cd MyApp
dotnet restore
dotnet run
```

The generated project contains:

```text
MyApp/
|-- MyApp.csproj
|-- Program.cs
|-- MainForm.cs
|-- MainForm.Designer.cs
`-- MainForm.mfdesign
```

`MainForm.cs` is the user-authored entry point for the form. `MainForm.Designer.cs` is generated
by the designer. `MainForm.mfdesign` stores the designer document model.

The template marks the designable file with:

```xml
<Compile Update="MainForm.cs">
  <ModernFormsNextDesigner>true</ModernFormsNextDesigner>
  <SubType>ModernFormsNextForm</SubType>
</Compile>
```

Do not use `<SubType>Form</SubType>` for ModernFormsNext forms. Visual Studio treats that value
as a classic Windows Forms marker and may try to open the built-in WinForms designer.

## Install the Visual Studio Designer

Install `ModernFormsNextDesigner.vsix` version `1.10.0` from the matching 1.10.0 release assets when
available. The extension version matches the framework because this release includes UserControl
design roots, safe custom previews, Shape editors, animation/effect editors, and the Padding parity
fix. Do not install an older VSIX when validating the 1.10.0 designer workflow. During
local repository development, build the VSIX project first:

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true
```

The development VSIX is produced at:

```text
ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix
```

For the normal Visual Studio instance, install the VSIX by double-clicking it or by using
`VSIXInstaller.exe`.

For the Experimental Instance, close Visual Studio and run:

```powershell
.\ModernFormsNext.VisualStudioExtension\Install-Experimental.ps1
```

The script removes stale ModernFormsNext Designer state from the Experimental Instance, installs
the current VSIX into `/RootSuffix Exp`, and forces Visual Studio to refresh package registration.
It also clears stale ModernFormsNext Designer registration from the Experimental Instance cache
and private registry, which prevents Visual Studio from loading an older deleted extension folder
after repeated local installs. The equivalent manual install command is:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" /rootSuffix:Exp ".\ModernFormsNext.VisualStudioExtension.Vsix\bin\Debug\net472\ModernFormsNextDesigner.vsix"
```

## Use the Designer

1. Open a ModernFormsNext project generated from the template or one that uses the
   `ModernFormsNextDesigner=true` item metadata.
2. Right-click `MainForm.cs`.
3. Choose **View ModernFormsNext Designer**.
4. Edit the form in the designer.
5. Save the designer document.

The designer opens the companion `MainForm.mfdesign` document but treats `MainForm.cs` as the
user-facing project item. Saving the designer writes `MainForm.mfdesign` and regenerates
`MainForm.Designer.cs`. It does not rewrite `MainForm.cs`.

New generated code assigns the form through `Size`. Reverse parsing still accepts `ClientSize`
from older generated files, so opening and saving a pre-1.8.0 form does not require a manual source
edit solely for this change.

Auto-save is enabled by default in the designer settings. It can be disabled from the designer
settings dialog if manual saves are preferred.

### Designer Keyboard Shortcuts

The designer supports the same control-oriented shortcuts in the standalone playground and in
the Visual Studio-hosted designer:

| Shortcut | Action |
| --- | --- |
| `Delete` | Deletes the selected control from the design document. |
| `Ctrl+C` | Copies the selected control into the designer clipboard. |
| `Ctrl+V` | Pastes the copied control into the active container. |
| `Ctrl+D` | Duplicates the selected control next to the original with a unique field name. |

The shortcuts operate on the selected design control, not on generated C# text. When a property
value editor is active in the Property Grid, text editing keeps normal keyboard behavior so
`Delete`, `Ctrl+C`, and `Ctrl+V` apply to the edited value instead.

## Add a New Form in Visual Studio

The VSIX also installs a C# item template named **ModernFormsNext Form**.

1. Right-click the project in Solution Explorer.
2. Choose **Add** > **New Item**.
3. Search for **ModernFormsNext Form**.
4. Enter the form name, for example `SettingsForm.cs`.
5. Choose **Add**.

The item template creates:

```text
SettingsForm.cs
SettingsForm.Designer.cs
SettingsForm.mfdesign
```

`SettingsForm.cs` is the user-authored form class, `SettingsForm.Designer.cs` is generated
initialization code, and `SettingsForm.mfdesign` is the designer document. The template does not
use `<SubType>Form</SubType>`, so Visual Studio should not route the file to the built-in
Windows Forms designer. The ModernFormsNext designer command can still identify the file because
the generated `.mfdesign` companion file contains ModernFormsNext design metadata.

## Localization

The designer and Visual Studio extension currently include English and Polish UI strings. Use
the designer settings dialog to switch the standalone designer shell language. The Visual Studio
extension follows the current Visual Studio UI culture where possible.

## Android

The packaged project template and Visual Studio designer are Windows-first. Android support in
ModernFormsNext 1.10.0 is **Experimental**, uses an explicit activity/shared-surface host, and is not
part of the default `mfn-app` template. See [Android platform status](platforms/android.md) before
creating an Android evaluation project.

## Troubleshooting

- If Visual Studio opens the built-in Windows Forms designer, remove `<SubType>Form</SubType>`
  from the project item and use `ModernFormsNextDesigner=true` instead.
- If the command does not appear on `MainForm.cs`, verify that the project references
  `ModernFormsNext`, the file is not `*.Designer.cs`, and the item metadata is present.
- If the Experimental Instance uses an older VSIX, uninstall **ModernFormsNext Designer** from
  **Extensions / Manage Extensions**, rerun `Install-Experimental.ps1`, and start Visual Studio
  with `/RootSuffix Exp /updateconfiguration`.
- If Visual Studio reports that `ModernFormsDesignerPackage` failed to load and `ActivityLog.xml`
  points to a removed random folder under `AppData\Local\Microsoft\VisualStudio\...\Extensions`,
  close all Visual Studio instances and rerun `Install-Experimental.ps1`. The script deletes the
  stale Experimental Instance private registry so the package `CodeBase` is rebuilt from the
  newly installed VSIX.
- If **ModernFormsNext Form** does not appear in **Add New Item** after installing the VSIX,
  close Visual Studio and refresh the item-template cache:

  ```powershell
  & "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" /RootSuffix Exp /installvstemplates
  ```
