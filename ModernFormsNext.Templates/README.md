# ModernFormsNext Templates

Templates for creating new applications with **ModernFormsNext**.

ModernFormsNext is a code-first C#/.NET UI framework focused on a WinForms-like developer experience, custom rendering, and simple desktop application startup.

This package installs the default project template for creating a new ModernFormsNext application.

---

## Requirements

* .NET SDK 10
* Windows is currently the recommended target platform

The generated project targets:

```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

---

## Installation

Install the templates from NuGet.org:

```powershell
dotnet new install ModernFormsNext.Templates
```

Check that the template is available:

```powershell
dotnet new list mfn
```

---

## Create a new application

Create a new ModernFormsNext application:

```powershell
dotnet new mfn-app -n MyApp
```

Open the generated project folder:

```powershell
cd MyApp
```

Restore packages:

```powershell
dotnet restore
```

Build the application:

```powershell
dotnet build
```

Run the application:

```powershell
dotnet run
```

---

## Template short name

The template short name is:

```text
mfn-app
```

Example:

```powershell
dotnet new mfn-app -n MyFirstApp
```

---

## Generated project structure

The generated project contains a minimal starter application:

```text
MyApp/
├─ MyApp.csproj
├─ Program.cs
└─ MainForm.cs
```

`Program.cs` contains the application entry point.

`MainForm.cs` contains the main application window.

The generated application is intentionally small. It is meant to be a clean starting point for a new ModernFormsNext app, not a full control gallery.

---

## Package references

The generated application uses NuGet package references.

Example:

```xml
<ItemGroup>
  <PackageReference Include="ModernFormsNext" Version="1.1.0" />
</ItemGroup>
```

---

## Troubleshooting

### The template is not visible

Run:

```powershell
dotnet new list mfn
```

If the template is missing, reinstall it:

```powershell
dotnet new uninstall ModernFormsNext.Templates
dotnet new install ModernFormsNext.Templates
```

### The generated project cannot restore packages

Make sure the referenced ModernFormsNext package version exists on NuGet.org.

Example:

```xml
<PackageReference Include="ModernFormsNext" Version="0.1.0-preview.1" />
```

### The generated project starts as a console application

The generated project should use:

```xml
<OutputType>WinExe</OutputType>
```

---

## Links

Repository:

```text
https://github.com/ProGraMajster/ModernFormsNext
```

Main package:

```text
ModernFormsNext
```

Template package:

```text
ModernFormsNext.Templates
```
