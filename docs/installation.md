# Installing ModernFormsNext

ModernFormsNext is distributed as NuGet packages, project templates, and a Visual Studio
designer extension. The framework remains code-first, but the designer can edit the companion
`.mfdesign` document and regenerate the matching `.Designer.cs` file.

## Requirements

- .NET SDK `10.0.201` or a compatible .NET 10 SDK feature band.
- Windows for the current desktop backend and Visual Studio designer experience.
- Visual Studio 2022/2026 for the VSIX designer extension.

## Install the Framework Package

For an existing application, reference the framework package:

```powershell
dotnet add package ModernFormsNext --version 1.7.0
```

The package provides the runtime controls, forms, rendering, layout, input, dialogs, and
Windows backend integration used by ModernFormsNext applications.

## Install the Project Template

Install the template package:

```powershell
dotnet new install ModernFormsNext.Templates::1.7.0
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

Install `ModernFormsNextDesigner.vsix` from the matching release assets when available. During
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

The script uninstalls older copies, installs the current VSIX into `/RootSuffix Exp`, and forces
Visual Studio to refresh package registration. The equivalent manual install command is:

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

Auto-save is enabled by default in the designer settings. It can be disabled from the designer
settings dialog if manual saves are preferred.

## Localization

The designer and Visual Studio extension currently include English and Polish UI strings. Use
the designer settings dialog to switch the standalone designer shell language. The Visual Studio
extension follows the current Visual Studio UI culture where possible.

## Troubleshooting

- If Visual Studio opens the built-in Windows Forms designer, remove `<SubType>Form</SubType>`
  from the project item and use `ModernFormsNextDesigner=true` instead.
- If the command does not appear on `MainForm.cs`, verify that the project references
  `ModernFormsNext`, the file is not `*.Designer.cs`, and the item metadata is present.
- If the Experimental Instance uses an older VSIX, uninstall **ModernFormsNext Designer** from
  **Extensions / Manage Extensions**, rerun `Install-Experimental.ps1`, and start Visual Studio
  with `/RootSuffix Exp /updateconfiguration`.
