# Getting Started with ModernFormsNext

## Quick Start

The easiest way to get started is by cloning the repository and running one of the sample applications.

```bash
git clone https://github.com/ProGraMajster/ModernFormsNext.git
cd ModernFormsNext
dotnet build
```

## Run the sample application

Example:
```bash
cd samples/ControlGallery
dotnet run
```

Other samples:

```bash
cd samples/Explorer
dotnet run
```

```bash
cd samples/Outlaw
dotnet run
```

## Create a New Application

### 1. Create project
```bash
dotnet new console -n MyApp
cd MyApp
```
### 2. Modify project file
Edit `.csproj`:
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
```
### 3. Add reference to ModernFormsNext
If using source (recommended for now):
```xml
<ItemGroup>
    <ProjectReference Include="..\ModernFormsNext\ModernFormsNext.csproj" />
    <ProjectReference Include="..\..\ModernFormsNext.WindowKit.Backend.Windows\ModernFormsNext.WindowKit.Backend.Windows.csproj" />
</ItemGroup>
```

### 4. Create Main Form
```csharp
using ModernFormsNext;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "My App";
        Width = 800;
        Height = 600;
    }
}
```

### 5. Program.cs
```csharp
class Program
{
    static void Main(string[] args)
    {
        Application.Run(new MainForm());
    }
}
```

## Notes
- There is currently no NuGet package
- Templates are not available yet
- The API may change as the project evolves

## Learn More

The best way to understand the framework is by exploring the sample projects:

`ControlGallery`
`Explorer`
`Outlaw`