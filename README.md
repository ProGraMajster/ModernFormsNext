# ModernFormsNext

[![.NET](https://github.com/ProGraMajster/ModernFormsNext/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ProGraMajster/ModernFormsNext/actions/workflows/dotnet.yml)

> Early-stage project. Use at your own risk.

See [CHANGELOG.md](CHANGELOG.md) for release notes and migration-relevant changes.

ModernFormsNext is a modern, code-first UI framework for .NET inspired by [Modern.Forms](https://github.com/modern-forms/Modern.Forms) and WinForms.

It focuses on performance, simplicity, and full control over UI without relying on XAML.

ModernFormsNext is not WPF, MAUI, WinUI, Avalonia, Uno, Blazor, Electron, or XAML. Framework UI is rendered by ModernFormsNext controls, not by native WinForms controls.

## Features

- WinForms-like API with no XAML
- SkiaSharp-based rendering
- Fully code-driven UI
- Optional ModernFormsNext Designer with `.mfdesign` documents and `.Designer.cs` generation
- WinForms-like data binding primitives
- Notification area icon support through backend services
- Extensible architecture
- Custom controls support
- Platform-neutral framework code with platform-specific backends

## Getting Started

### Requirements

- .NET SDK `10.0.201` as configured by `global.json`.
- SDK roll-forward is enabled for the latest installed .NET 10 feature band.
- Windows is currently the primary and best-supported runtime target.

Use `ModernFormsNext.slnx` for solution-level restore and build commands.

### Clone

```powershell
git clone https://github.com/<your-username>/ModernFormsNext.git
cd ModernFormsNext
```

### Restore

```powershell
dotnet restore .\ModernFormsNext.slnx
```

### Build

```powershell
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true
```

### Run ControlGallery

`samples/ControlGallery` is the manual visual test app for controls, layout, rendering, focus, input, and theme behavior.

```powershell
dotnet run --project .\samples\ControlGallery\ControlGallery.csproj
```

### Run the Template Reference App

`samples/ModernFormsNext.DemoApp` is the reference application for the Visual Studio extension/template experience. Keep it small, beginner-friendly, and representative of the generated application structure. Do not use it as a playground for random control experiments.

```powershell
dotnet run --project .\samples\ModernFormsNext.DemoApp\ModernFormsNext.DemoApp.csproj
```

## Install the Framework and Designer

For packaged app development:

```powershell
dotnet add package ModernFormsNext --version 1.7.0
dotnet new install ModernFormsNext.Templates::1.7.0
dotnet new mfn-app -n MyApp
```

The generated template includes `MainForm.cs`, generated `MainForm.Designer.cs`, and companion
designer metadata in `MainForm.mfdesign`. Install the Visual Studio extension package
`ModernFormsNextDesigner.vsix` to use **View ModernFormsNext Designer** from `MainForm.cs`.

See [Installing ModernFormsNext](docs/installation.md) for the full package, template, VSIX,
Experimental Instance, and troubleshooting instructions.

## Basic Code-First Example

```csharp
using ModernFormsNext;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    public MainForm()
    {
        Text = "Hello ModernFormsNext";
        Width = 800;
        Height = 600;
    }
}
```

## Repository Structure

- `ModernFormsNext/` - shared framework code: controls, forms, rendering, layout, input, themes, and dialogs.
- `ModernFormsNext.Designing/` - neutral designer document model, metadata, serialization, and validation.
- `ModernFormsNext.CodeGeneration/` - `.mfdesign` to `.Designer.cs` generation and conservative reverse parsing.
- `ModernFormsNext.Designer/` - reusable designer shell used by the playground and Visual Studio extension.
- `ModernFormsNext.VisualStudioExtension/` - Visual Studio command, detector, editor, and VSIX integration.
- `ModernFormsNext.WindowKit/` - platform-neutral windowing, dispatcher, input, storage, and service abstractions.
- `ModernFormsNext.WindowKit.Backend/` - shared backend bootstrap and interop infrastructure.
- `ModernFormsNext.WindowKit.Backend.Windows/` - Windows backend implementation and Win32 interop.
- `ModernFormsNext.Templates/` - project templates for generated user applications.
- `samples/ControlGallery/` - manual visual validation app for controls and rendering.
- `samples/ModernFormsNext.DemoApp/` - reference/template application generated for users, not a control playground.
- `docs/` - architecture, getting started, data binding, platform-specific features, samples, and screenshots.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Installation and Designer](docs/installation.md)
- [Designer Architecture](docs/designer-architecture.md)
- [Data Binding](docs/data-binding.md)
- [Platform-Specific Features](docs/platform-specific-features.md)
- [Samples](docs/samples.md)
- [Architecture](docs/architecture.md)

## Templates and Packages

The repository contains NuGet package metadata and a `ModernFormsNext.Templates` template package project. For repository development, prefer project references and the solution commands above. For template validation, keep `ModernFormsNext.Templates` and `samples/ModernFormsNext.DemoApp` aligned so generated applications show the recommended startup pattern.

## Project Direction

ModernFormsNext is:

- not just a fork
- a separate evolution path
- a place for experimentation and new ideas

It may include:

- architectural changes
- performance improvements
- new controls
- platform-specific features

## Third-Party Code and Licensing

This repository contains or may contain code derived from other MIT-licensed projects. See [third-party-licenses.md](third-party-licenses.md) for details. The original `Modern.Forms` files also document third-party sources such as Avalonia, Mono WinForms, and Microsoft WinForms.

## License

This project is distributed under the MIT License. See [license.md](license.md).
