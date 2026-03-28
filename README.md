# ModernFormsNext
[![.NET](https://github.com/ProGraMajster/ModernFormsNext/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ProGraMajster/ModernFormsNext/actions/workflows/dotnet.yml)

> ⚠️ Early-stage project. Use at your own risk.

ModernFormsNext is a modern, code-first UI framework for .NET inspired by Modern.Forms and WinForms.

It focuses on performance, simplicity, and full control over UI without relying on XAML.

---

## ✨ Features

- WinForms-like API (no XAML)
- SkiaSharp-based rendering
- High performance compared to MAUI
- Fully code-driven UI
- Extensible architecture
- Custom controls support

---

## 🚀 Getting Started

### Requirements

- .NET 8 / .NET 10 (recommended)
- Windows (currently best supported)

---

### Clone

```bash
git clone https://github.com/<your-username>/ModernFormsNext.git
cd ModernFormsNext
```
## Bulid
```bash
dotnet build
```
## Run Sample
```bash
cd samples/ControlGallery
dotnet run
```

## Basic Example
```csharp
using ModernFormsNext;

class Program
{
    static void Main(string[] args)
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

You can describe your solution here, for example:

- `src/` - framework source code
- `samples/` - sample applications
- `docs/` - documentation and screenshots

## 🧠 Project Direction

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
