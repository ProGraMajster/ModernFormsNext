# Releasing ModernFormsNext

This repository creates releases from Git tags, not from ordinary pushes to `master`.

> [!WARNING]
> Pushing a tag that matches `v*.*.*` starts `.github/workflows/release.yml`. That workflow creates
> a GitHub Release and publishes `.nupkg` packages to NuGet. A release tag is therefore a publication
> action, not a harmless marker.

## Release versions

NuGet package versions use SemVer without a `v` prefix. The shared value is stored in
`Directory.Build.props`:

```xml
<ModernFormsNextPackageVersion>1.10.0</ModernFormsNextPackageVersion>
```

Git tags use the same version with a `v` prefix:

```text
v1.10.0
```

The Visual Studio extension version is stored separately so an emergency extension-only patch
remains possible:

```xml
<ModernFormsNextVisualStudioExtensionVersion>1.10.0</ModernFormsNextVisualStudioExtensionVersion>
```

For coordinated framework minor/major releases, the VSIX version must match the framework release.
An intentionally independent VSIX patch is permitted only when it is documented in the changelog
and does not imply a mismatched framework compatibility claim.

Keep the VSIX value synchronized with:

- `ModernFormsNext.VisualStudioExtension.Vsix/source.extension.vsixmanifest`;
- `InstalledProductRegistration` in `ModernFormsNext.VisualStudioExtension/ModernFormsDesignerPackage.cs`;
- `InstalledProductRegistration` in `ModernFormsNext.VisualStudioExtension.Vsix/ModernFormsDesignerPackage.cs`.

The VSIX packaging project derives `Version`, `AssemblyVersion`, and `FileVersion` from the central
property and validates the final archive against it. Do not change the VSIX Identity, Product ID,
publisher, Package ID, or Marketplace identity during an ordinary release.

## Assembly version strategy

Library projects use the .NET SDK version defaults. Package `Version` and `FileVersion` follow the
release; `InformationalVersion` may also include source revision information. `AssemblyVersion`
remains at `major.minor.0.0` for compatible patch releases to avoid unnecessary binary binding
breaks. The VSIX assembly/file versions use the four-part form `major.minor.patch.0`.

## Choose the next version

Use SemVer:

- `1.0.0 -> 1.0.1` for a compatible bug or documentation fix;
- `1.0.0 -> 1.1.0` for backward-compatible features or public API additions;
- `1.0.0 -> 2.0.0` for intentional breaking public API changes;
- `1.1.0 -> 1.2.0-preview.1` for a prerelease following the repository's existing convention.

NuGet versions cannot be republished. Confirm that the intended version is unused before creating
the tag.

## Prepare the release commit

1. Set `ModernFormsNextPackageVersion` and the coordinated VSIX version.
2. Synchronize the application template package reference and any versioned installation examples.
3. Write the release section in `CHANGELOG.md` from the last release tag to the intended release
   commit. Keep it `Unreleased` until a publication date is deliberately chosen.
4. Confirm package metadata, license, README/icon inclusion, Source Link, XML documentation, and
   symbol-package policy.
5. Confirm the VSIX manifest, registration attributes, assets, prerequisites, and Visual Studio
   installation targets.
6. Review platform claims. For 1.10.0, Android must remain explicitly **Experimental** and must not
   be described as a complete `Application.Run(Form)` or WindowKit implementation.
7. Review `git diff`, stage only the intended files, and create focused commits. Do not use
   `git add .` without auditing the entire worktree.

## Local validation

Use `ModernFormsNext.slnx`:

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet build .\ModernFormsNext.slnx --configuration Debug --no-restore /p:EnableWindowsTargeting=true
dotnet build .\ModernFormsNext.slnx --configuration Release --no-restore --verbosity normal -m:1 /p:UseSharedCompilation=false
dotnet test .\ModernFormsNext.slnx --configuration Debug --no-restore
```

Repository-level structure and version tests use Git-tracked files when available and a bounded
filesystem fallback for source archives. Generated directories and nested worktrees are not release
inputs; see [Repository validation inputs](docs/repository-validation.md) before adding a new
repository-wide traversal. A clean external exact-SHA worktree remains recommended for provenance,
while `-m:1 /p:UseSharedCompilation=false` remains necessary to avoid genuine concurrent writes to
shared MicroCom intermediate outputs.

For 1.10.0, additionally validate:

- `net10.0-windows` framework and samples;
- the `net10.0-android` backend, cross-platform sample, and Android backend tests when the workload
  is installed;
- designer and dynamic-resource test suites;
- VSIX Debug and Release builds and the embedded manifest version;
- all intended `.nupkg` and `.snupkg` outputs;
- package IDs/versions, README, icon, XML documentation, Source Link metadata, and the absence of
  `bin`, `obj`, IDE state, and temporary files;
- the Windows template output and, when a device is available, Android touch/IME/lifecycle behavior.

Package into a clean, ignored output directory:

```powershell
dotnet pack .\ModernFormsNext.slnx --configuration Release --no-build --output .\.codex-pack
.\scripts\Validate-ReleasePackages.ps1 -PackageDirectory .\.codex-pack
```

The template package intentionally has no `.snupkg`; published library packages should have one.

Versioned documentation and sample artifacts are also mandatory release inputs. Restore the pinned
DocFX tool, run the script tests, build the four ZIP files from the Release outputs, and validate
them before creating a tag:

```powershell
dotnet tool restore
$commit = (git rev-parse HEAD).Trim()
.\scripts\tests\Test-ReleaseDocumentation.ps1
.\scripts\Build-ReleaseDocumentation.ps1 `
    -Version 1.10.0 `
    -Tag local `
    -Commit $commit `
    -OutputDirectory .\artifacts\release-docs
.\scripts\Validate-ReleaseDocumentation.ps1 `
    -ArtifactDirectory .\artifacts\release-docs `
    -ExpectedVersion 1.10.0 `
    -ExpectedCommit $commit `
    -ExpectedTag local
```

For a prerelease dry-run before its matching release-notes file exists, use the explicit local-only
`-ReleaseNotesPath` override documented in
[`docs/releasing/versioned-documentation-artifacts.md`](docs/releasing/versioned-documentation-artifacts.md).
The tag workflow never uses that override: it requires `docs/<version>-release-notes.md`, confirms
the tag resolves to `github.sha`, and validates every archive before publication.

## Publication workflow

After the release commit is reviewed and the normal `.NET` workflow is green:

1. Replace `Unreleased` with the actual release date in `CHANGELOG.md` and update the 1.10.0 link
   from a comparison URL to the final tag URL.
2. Commit that final release-note change and push it through the normal review path.
3. Create the annotated or lightweight `vX.Y.Z` tag on the exact reviewed commit.
4. Push the tag only when GitHub Release and NuGet publication are intended.
5. Monitor the `Release` workflow until GitHub Release creation and every NuGet push succeed.
6. Verify the matching docs, offline HTML, samples, and SDK ZIP files are attached to the GitHub
   Release and contain the expected tag/commit metadata.
7. Attach the matching `ModernFormsNextDesigner.vsix` to the GitHub Release. The current workflow
   uploads NuGet packages and symbols but does not upload the VSIX automatically.
8. Verify the public NuGet indexes, package contents, GitHub assets, and VSIX version after
   publication.

Example commands are intentionally explicit:

```powershell
git status
git log --oneline --decorate -n 20
git tag v1.10.0
git push origin v1.10.0
```

Do not run the tag or push commands until the release is approved. Never force-push or move a
published release tag. If publication fails after a package reaches NuGet, prepare a new patch
version rather than trying to reuse the published version.

## Diagnostics

List local tags:

```powershell
git tag --list
```

List tags on GitHub:

```powershell
git ls-remote --tags origin
```

If the workflow did not start, verify that the tag exists on `origin` and matches `v*.*.*`. If the
workflow starts unexpectedly, do not delete/recreate the tag blindly: first inspect whether a
GitHub Release or NuGet package was already published.
