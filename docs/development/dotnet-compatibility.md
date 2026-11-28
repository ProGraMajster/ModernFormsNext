# .NET compatibility for ModernFormsNext 2.0 development

The framework builds and packs against .NET 10 LTS. A Windows application can
target `net10.0-windows` or `net11.0-windows` and consume the same .NET 10
library assets. Stage 1 validates Windows x64 on both runtimes. Keep the
application on a serviced patch of its chosen runtime. Check the supported
Windows versions for that runtime.
[Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) ·
[.NET 11 supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/11.0/supported-os.json)

## Choose the application target

| Area | .NET 10 LTS | .NET 11 STS |
| --- | --- | --- |
| Windows x64 desktop | Default project template and source build | Opt-in project template and focused compatibility checks |
| Android ARM64 | Experimental, matching API 36 workload, minimum API 23 | Experimental consumer probe with CoreCLR, API 37 workload and minimum API 24. Published Android assets and release qualification use the .NET 10 workload. |
| Visual Studio project targeting | VS 2026 with an SDK feature band appropriate for the installation | VS 2026 18.12 and SDK 11.0.1xx |
| Ordinary Designer and VSIX | .NET 10 Windows out-of-process host, .NET Framework 4.7.2 VSIX adapter | Use the standard data Designer in VS 2026 18.12 with the same .NET 10 host. Project source tooling follows the syntax limits below. |

Install SDKs side by side. The checkout's `global.json` selects SDK 10.0.401
or a later .NET 10 feature-band build. For a .NET 11 consumer, use a project
directory outside that SDK pin or a separate `global.json` selecting a
serviced SDK 11.0.1xx. Check `dotnet --version` in the project directory.
C# 15 is the default for a .NET 11 application. Framework sources and
generated starter code use syntax that also builds with SDK 10.
[SDK, MSBuild and Visual Studio versioning](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs) ·
[Announcing .NET 11](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/)

```powershell
dotnet new mfn-app -n MyApp --FrameworkVersion net11.0-windows
```

For an existing Windows project, change its application TFM to
`net11.0-windows` with any required Windows version suffix, then restore,
build and test on CLR 11. NuGet selects the matching lower-version Windows
asset and its backend dependency. The ordinary `mfn-app` command keeps
`net10.0-windows`. VSIX item templates add files to the active project using
its existing target. The manifest's VS install eligibility, the out-of-process
host's .NET 10 runtime, and the application's TFM are separate requirements.
Consult [Designer support](../visual-studio-designer-host.md) and issue [#44](https://github.com/ProGraMajster/ModernFormsNext/issues/44) for IDE qualification.

### SDK and language scope

| Use | Stage 1 status |
| --- | --- |
| Consume the runtime packages | Windows x64 apps targeting `net10.0-windows` or `net11.0-windows` use the published TFM layout built with SDK 10. |
| Build and pack the framework checkout | SDK 10 remains the repository and release toolchain. An SDK 11 source build has a separate qualification scope. |
| Use C# 15 in application code | SDK 11 compiles it for `net11.0`. The framework source and starter files use the existing language policy. |
| Parse project source in the Designer | The standard host ships Roslyn 4.14.0. A C# 15 union compiled with SDK 11 and produced syntax errors in this parser in both default and Preview modes. Use supported syntax in files handled by the Designer. |

Broader Designer syntax qualification is tracked separately. [C# 15 Designer issue #555](https://github.com/ProGraMajster/ModernFormsNext/issues/555)

.NET 11 raises the x86/x64 minimum instruction set to x86-64-v2 and the
Windows Arm64 baseline to include LSE. Windows/Linux x64 ReadyToRun uses
x86-64-v3 where applicable. Keep .NET 10 for eligible deployments that cannot
move to the .NET 11 hardware baseline. Stage 1 tested Windows x64 and Android
ARM64. Windows Arm64 and x86 need their own native-asset and runtime checks.
[Microsoft .NET 11 runtime notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime)

### Android experiment

Use a separate sample directory and SDK 11, `net11.0-android`, Android API
37 and minimum API 24. Remove explicit `RunAOTCompilation` settings when
using CoreCLR. Use an isolated NuGet source mapping for all
`ModernFormsNext*` packages so the app tests the intended candidate.
The local probe used installed Android workload 37.2.29, JDK 25 and an
ARM64 handset on API 36. It restored the existing `net10.0-android36.0`
backend and built an embedded-assemblies APK. The successful build used
Android build tools 36.0.0 after AAPT2 reported invalid resource stamp paths
on the first attempt. Preserve the .NET 10 app's API 23 floor when testing
that path. Follow [Android platform status](../platforms/android.md) for
the release baseline and known framework restrictions.
[.NET for Android releases](https://github.com/dotnet/android/releases) ·
[Android build properties](https://github.com/dotnet/android/blob/main/Documentation/docs-mobile/building-apps/build-properties.md)

## Project inventory

The table lists every tracked project from the 1.12.1 baseline. Project TFMs
in the second column apply before and after Stage 1 unless the row gives
a new generated target. `10` denotes `net10.0`, `10W` denotes
`net10.0-windows`, `10WN` denotes `net10.0-windows10.0.26100.0`, and
`10A` denotes `net10.0-android`. .NET 11 asset compatibility and verified
runtime scenarios are recorded separately.

| Project | TFM | Dependencies and platform | .NET 10 and .NET 11 validation |
| --- | --- | --- | --- |
| ModernFormsNext | 10, 10W | WindowKit, Backend, Windows backend on 10W, Markdig 1.3.2, RichTextKit 0.4.167 | 10 LTS. Verified 11 Windows and bounded neutral probes. Test NuGet selection, startup and UI. |
| ModernFormsNext.WindowKit | 10 | Backend, SkiaSharp 4.158.0 and Linux native assets, System.Drawing.Common 10.0.10 | 10 API floor. Transitive 11 assets exercised in Windows and Android probes. Test native resolution per RID. |
| ModernFormsNext.WindowKit.Backend | 10 | Shared backend infrastructure | 10 API floor. Transitive 11 Windows and Android probes. |
| ModernFormsNext.WindowKit.Backend.Windows | 10 | WindowKit, Backend, SkiaSharp, System.Drawing.Common, Win32 | Windows only despite neutral TFM. Package launch and focused native tests on 10 and 11 x64. |
| ModernFormsNext.WindowKit.Backend.Android | 10, 10A | WindowKit, Backend, Android bindings on 10A, SkiaSharp.Views 4.158.0, System.Formats.Nrbf 10.0.10 | Experimental API 23 on SDK 10. API 24+ consumer probe on 11. Check APK and touch, IME and lifecycle on device. |
| ModernFormsNext.Designing | 10 | Design document schema, no direct package dependencies | 10 SDK and portable APIs. 11 compatibility by TFM. Exercise JSON and round trip. |
| ModernFormsNext.CodeGeneration | 10 | Designing, Roslyn C# 4.14.0 | 10 SDK and portable APIs. 11 compatibility by TFM. Exercise generated C# compilation. |
| ModernFormsNext.Designer | 10W | Core, Designing, CodeGeneration, Roslyn | Standard 10 Windows Designer graph. Retain host-bound tests and IDE checks. |
| ModernFormsNext.DesignerPreviewWorker | 10W | Core, Windows x64 sandbox qualification | Private 10 tooling. Preview qualification stays in #40 and #486. |
| ModernFormsNext.VisualStudioDesignerHost | 10W | Core, Designer, Designing, shared VS protocol | 10 Windows host. Build and verify packaged host runtime and IPC. |
| ModernFormsNext.VisualStudioExtension | 10W | Designer stack, shared protocol, Windows backend, Roslyn, MessagePack, VS SDK 17.14 | Host tooling for 10. Verify ordinary data preview, save and recovery across IDE versions. |
| ModernFormsNext.VisualStudioExtension.Shared | netstandard2.0 | Shared IPC contract | Shared by .NET Framework adapter and .NET 10 host. Verify protocol across host boundary. |
| ModernFormsNext.VisualStudioExtension.Vsix | net472 | VS SDK 17.14, shared protocol and 10 host payload | VS tooling. Check package install eligibility separately from project TFM. |
| ModernFormsNext.Automation | 10 | Core, portable automation | 10 and 11 exercised by runner experiment. Test action and lifecycle contracts. |
| ModernFormsNext.Automation.Windows | 10W | Automation, Windows bridge | 10 package. 11 TFM-compatible. Full live bridge qualification outstanding on 11. |
| ModernFormsNext.Automation.Cli | 10W | Automation.Windows | Windows tool, uses CLR 10. Test with owned bridge hosts. |
| ModernFormsNext.Testing | 10 | Core, WindowKit | 10 and 11 exercised through runner experiment. Headless host only. |
| ModernFormsNext.SystemNotifications.AppSdk | 10WN | Windows Backend, Windows App SDK Foundation/Interactive Experiences/Runtime | 10 package retained. 11 needs matching platform suffix and native activation verification. |
| ModernFormsNext.SystemNotifications.WinRT | 10WN | Windows Backend, WinRT | 10 package retained. 11 needs matching platform suffix and activation verification. |
| ModernFormsNext.Templates | 10 | Content package, CLI template engine | Pack with SDK 10 and generate under SDKs 10 and 11. |
| Template MyApp | 10W → default 10W; choices 10 and net11.0-windows | Package reference to ModernFormsNext | Fresh Windows package restore, build and launch on 10 and 11. Neutral option requires a compatible backend. |
| ModernFormsNext.Tests | 10 | Core, VSTest/xUnit 2 | Release baseline on 10. Focused picker class on 10 and 11. |
| ModernFormsNext.Testing.Tests | 10 | Testing, VSTest/xUnit 2 | 10 release qualification. Retain headless contract tests. |
| ModernFormsNext.Automation.Tests | 10 | Automation, Testing, VSTest/xUnit 2 | 10 release qualification. Isolated 11 xUnit/MTP comparison. |
| ModernFormsNext.Automation.Windows.Tests | 10W | Bridge, CLI and TestHost, VSTest/xUnit 2 | 10 release qualification. Windows native companion processes. |
| ModernFormsNext.Automation.Windows.TestHost | 10W | Automation.Windows and Core | Windows child host for bridge tests on 10. |
| ModernFormsNext.CrossPlatform.Sample.Tests | 10W | CrossPlatform.Sample, Testing, VSTest/xUnit 2 | Desktop tests on 10. Physical Android uses a separate path. |
| ModernFormsNext.Designer.Tests | 10W | Designing, CodeGeneration, Designer and VS host/extension, VSTest/xUnit 2 | 10 source/serialization and native snapshot tests. Host qualification uses 10. |
| ModernFormsNext.VisualStudioExtension.Vsix.Tests | net472 | VSIX, VS SDK, VSTest/xUnit 2 | .NET Framework on Windows, independent of application TFM. |
| ModernFormsNext.WindowKit.Backend.Android.Tests | 10 | Portable Android code and Core, VSTest/xUnit 2 | 10 desktop mapping/lifecycle tests. Device checks are separate. |
| ModernFormsNext.WindowKit.Backend.Windows.Tests | 10W | Core, Windows backend, UIA host/client, VSTest/xUnit 2 | Full 10 baseline and focused CLR 11 reuse under SDK 11. |
| ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationClient | 10W | Windows UIA interop | Child executable for native Windows test runs. |
| ModernFormsNext.WindowKit.Backend.Windows.Tests.UiAutomationHost | 10W | Core, Designer | Native scenario host. CLR 10 and 11 paint, scaling and input probes. |
| ControlGallery | 10W | Core and Windows backend | Windows manual visual checks on 10; targeted 11 checks use package consumer/native test host. |
| Explore | 10 | Core and explicit Windows backend | Windows sample on 10. Runtime 11 execution unqualified. |
| ModernFormsNext.Android.SmokeTest | 10A | Android backend | Experimental SDK 10 device smoke. Matching 11 probe used CrossPlatform.Sample. |
| ModernFormsNext.CrossPlatform.Sample | 10W, 10A | Core, Android backend, Android HarfBuzzSharp 15.0.0 | 10 baseline. Independent NuGet-only Android 11 APK probe. |
| ModernFormsNext.DemoApp | 10W | Core | Default 10 template reference. Test generated 11 variant separately. |
| ModernFormsNext.DesignerPlayground | 10W | Core, Designer | 10 Windows tooling sample. Test standard Designer workflows. |
| Outlaw | 10 | Core and explicit Windows backend | Windows sample on 10. Runtime 11 execution unqualified. |
| SystemNotifications sample | 10WN | Core, AppSdk and WinRT notification providers | Optional Windows activation scenarios. Test foreground/background on deployed OS. |
| MicroComGenerator | 10 | Roslyn Workspaces 4.0.1, CommandLineParser 2.9.1 | Build tool. Regenerate and compare only when interop inputs change. |
| MicroComPatcher | 10 | Mono.Cecil 0.11.6 | Build tool. Validate managed/native signatures with existing MicroCom checks. |
| BuildGraphAudit | 10 | .NET command line tooling | Source tooling. Run targeted audit when graph or path forwarding changes. |
| DesignerExecutablePreview ProjectFixture | 10W | Core, private worker tests | Private qualification fixture for #40 and #486. |
| DynamicResourcePublish | 10 | Core and typed resource fixture | Test JIT and trim of bounded resource scenario. 11 package probe uses the same source. |
| PartialRedrawGallery | 10W | ControlGallery | Windows performance fixture. Use for physical monitor and partial-redraw checks. |

## Packaging and publishing

The SDK 10 pack continues to emit the existing `lib` groups, including the
core Windows and neutral group and the experimental Android group. NuGet
restores external RID assets from dependencies. The candidate validator
checks the archive layout and the generated Windows consumers check the
selected asset and actual Skia native load. Publishing a new TFM-specific
library would add a package and API compatibility surface. Revisit that
choice if a required .NET 11 API or dependency needs a separate build.

The runtime API and assembly version follow the existing SDK 10 source.
No conditional API, source-generator migration, serializer change, trim
annotation or native binary change is needed for this stage. CodeGeneration
uses Roslyn for Designer code generation and parsing. The VSIX ships the
existing data-preview path. Trimming evidence is limited to the
DynamicResource fixture. The Windows x64 NativeAOT probe with process-local
MSVC tools generated native code and reached linking. The installed toolchain
lacks the Windows SDK import libraries, and linking stopped with LNK1181
for `advapi32.lib`. Entire-framework trim and NativeAOT qualification is
tracked in [#336](https://github.com/ProGraMajster/ModernFormsNext/issues/336#issuecomment-7223849755).

Keep development commits at the existing publication identity while working
locally against isolated candidate feeds. Set coordinated NuGet
`2.0.0-preview.1` and numeric VSIX identity when preparing an approved
prerelease, following [RELEASING.md](../../RELEASING.md). Update assembly
identity at the major release boundary under that policy. Integrate Stage 1
changes through PRs to `develop/2.0.0` using the [CI policy](../ci.md).
