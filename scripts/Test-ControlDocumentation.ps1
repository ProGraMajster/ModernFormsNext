[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$guideRoot = Join-Path $root 'docs/controls'
$fixture = Join-Path $root "artifacts/control-documentation/$([Guid]::NewGuid().ToString('N'))"
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$reference = [Security.SecurityElement]::Escape((Join-Path $root 'ModernFormsNext/ModernFormsNext.csproj'))
$project = Join-Path $fixture 'ControlDocumentation.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$reference" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8

$checks = [Collections.Generic.List[string]]::new()
$examples = [Collections.Generic.List[string]]::new()
$covered = [Collections.Generic.List[string]]::new()
$exampleCount = 0
$toc = Get-Content -LiteralPath (Join-Path $root 'docs-site/toc.yml') -Raw
$index = Get-Content -LiteralPath (Join-Path $guideRoot 'README.md') -Raw

foreach ($file in Get-ChildItem -LiteralPath $guideRoot -File -Filter '*.md' | Sort-Object Name) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $type = [regex]::Match($content, '(?m)^Type \x60(ModernFormsNext[.\w]+)\x60').Groups[1].Value
    if (!$type) {
        if ($file.Name -notin @('README.md', 'control-fundamentals.md', 'components.md')) {
            throw "Missing type declaration in $($file.Name)."
        }
        continue
    }
    if ($covered.Contains($type)) { throw "Multiple guides for $type." }
    $covered.Add($type)
    if (!$toc.Contains("href: content/docs/controls/$($file.Name)") -or !$index.Contains("]($($file.Name))")) {
        throw "Missing navigation entry for $($file.Name)."
    }

    foreach ($heading in @('Quick start', 'Configuration example', 'Interaction example', 'Properties',
        'Methods and behavior', 'Events', 'Platform support', 'Designer support',
        'Accessibility and practical considerations', 'Related guides and API')) {
        if (![regex]::IsMatch($content, "(?m)^## $([regex]::Escape($heading))\r?$")) {
            throw "Missing section '$heading' in $($file.Name)."
        }
    }
    $checks.Add("VerifyGuide(typeof(global::$type));")
    foreach ($row in [regex]::Matches($content, '(?m)^\| \x60(\w+)\x60 \|')) {
        $property = $row.Groups[1].Value
        $checks.Add("VerifyProperty(typeof(global::$type), ""$property"");")
    }

    # Each fenced block continues the previous block in the same method.
    $blocks = [regex]::Matches($content, '(?ms)^\x60{3}csharp\r?\n(.*?)\r?\n^\x60{3}\r?$')
    if ($blocks.Count -lt 3) { throw "Missing examples in $($file.Name)." }
    $exampleCount += $blocks.Count
    $methodName = 'Example_' + $type.Replace('.', '_')
    $examples.Add("static async Task $methodName(Form form) {")
    foreach ($block in $blocks) {
        $body = $block.Groups[1]
        $line = 1 + [regex]::Matches($content.Substring(0, $body.Index), "\n").Count
        $examples.Add("#line $line ""$($file.FullName)""")
        $examples.Add($body.Value)
    }
    $examples.Add('#line default')
    $examples.Add('await Task.CompletedTask;')
    $examples.Add('}')
}

$verifier = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ModernFormsNext;
using ModernFormsNext.DataBinding;
using ModernFormsNext.Drawing;
using SkiaSharp;

internal static class Program
{
    private static readonly HashSet<Type> Guides = [];

    private static void Main()
    {
        // Compile each guide as ordinary C# without executing its UI statements.
        GUIDE_CHECKS
        var shipped = typeof(Control).Assembly.GetExportedTypes();
        var expected = shipped.Where(type => !type.IsAbstract
            && (typeof(Control).IsAssignableFrom(type) || typeof(WindowBase).IsAssignableFrom(type))).ToArray();
        var missing = expected.Where(type => !Guides.Contains(type)).ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException("Missing control or window guides: " + string.Join(", ", missing.Select(t => t.FullName)));
        Console.WriteLine($"Verified {expected.Count(t => typeof(Control).IsAssignableFrom(t))} concrete controls, "
            + $"{expected.Count(t => typeof(WindowBase).IsAssignableFrom(t))} windows and {Guides.Count} individual guides.");
    }

    private static void VerifyGuide(Type type)
    {
        if (!type.IsVisible || type.IsAbstract || !Guides.Add(type))
            throw new InvalidOperationException($"Invalid or duplicate guide for {type.FullName}.");
    }

    private static void VerifyProperty(Type type, string name)
    {
        if (!type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Any(property => property.Name == name))
            throw new MissingMemberException(type.FullName, name);
    }

    GUIDE_EXAMPLES
}
'@
$verifier = $verifier.Replace('GUIDE_CHECKS', ($checks -join [Environment]::NewLine)).
    Replace('GUIDE_EXAMPLES', ($examples -join [Environment]::NewLine))
$verifier | Set-Content -LiteralPath (Join-Path $fixture 'Program.cs') -Encoding utf8

& dotnet build $project -c $Configuration -m:1 /p:UseSharedCompilation=false /p:BuildProjectReferences=false
if ($LASTEXITCODE -ne 0) { throw "Control documentation examples failed to compile. See $fixture." }

& dotnet (Join-Path $fixture "bin/$Configuration/net10.0-windows/ControlDocumentation.dll")
if ($LASTEXITCODE -ne 0) { throw "Control documentation coverage validation failed. See $fixture." }

Write-Host "Compiled $exampleCount C# blocks."
