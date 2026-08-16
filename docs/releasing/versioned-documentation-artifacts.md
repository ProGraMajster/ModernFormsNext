# Versioned release documentation artifacts

ModernFormsNext GitHub Releases include offline documentation and sample archives generated from
the exact release tag. These assets let a user keep documentation that continues to match an
installed NuGet version after `master` changes.

The bundles supplement NuGet packages, symbols, templates, and the Visual Studio extension. They
do not replace those distribution formats and do not contain a renamed copy of the repository.

## Asset naming and contents

For version `X.Y.Z`, the release workflow creates:

| Asset | Purpose |
| --- | --- |
| `ModernFormsNext-X.Y.Z-docs.zip` | Markdown source, images, changelog, release notes, migration guides, DocFX landing/navigation source, Release XML documentation, and public API snapshots. |
| `ModernFormsNext-X.Y.Z-docs-html.zip` | Self-contained DocFX site with user articles, local search/theme assets, and API reference generated from Release assemblies. |
| `ModernFormsNext-X.Y.Z-samples.zip` | Selected user-facing Windows samples converted to exact-version NuGet references, plus the template application in a separate `reference` section. |
| `ModernFormsNext-X.Y.Z-sdk.zip` | Aggregate offline/reference bundle containing documentation source, HTML, samples, XML docs, public API snapshots, release notes, license, and metadata. |

Every ZIP has one versioned top-level directory. Every bundle contains `metadata/version.txt`,
`metadata/commit.txt`, and `metadata/release.json`. `release.json` uses this schema:

```json
{
  "schemaVersion": 1,
  "product": "ModernFormsNext",
  "bundle": "docs-html",
  "version": "1.10.0",
  "tag": "v1.10.0",
  "commit": "full-40-character-git-sha",
  "generatedAtUtc": "2026-08-16T12:00:00.000Z",
  "dotnetSdk": "10.0.201"
}
```

The timestamp intentionally records generation time. File selection and ZIP entry ordering are
stable, so repeated builds from the same inputs are semantically equivalent even when archive
timestamps differ.

## Source identity and release failure behavior

`.github/workflows/release.yml` checks out the tag event with full tag history. It derives the
version by removing the leading `v` from `github.ref_name`, then passes the tag and `github.sha` to
`Build-ReleaseDocumentation.ps1 -RequireTag`.

The build script verifies all of the following before creating bundles:

- the version is valid SemVer;
- `HEAD` equals the expected full commit SHA;
- the expected tag is exactly `v<version>`;
- that tag resolves to the expected commit;
- `docs/<version>-release-notes.md` exists;
- the required Release assemblies and XML documentation exist.

Script tests, bundle generation, archive validation, metadata validation, and offline-link checks
all run before `gh release create` and before NuGet authentication/publication. A documentation
failure therefore stops the release before any release asset or NuGet package is published.

## Local dry-run

Restore the repository and the pinned DocFX tool, build Release, then build and validate the
archives:

```powershell
dotnet restore .\ModernFormsNext.slnx
dotnet tool restore
dotnet build .\ModernFormsNext.slnx --configuration Release --no-restore --verbosity normal -m:1 /p:UseSharedCompilation=false

$commit = (git rev-parse HEAD).Trim()
.\scripts\tests\Test-ReleaseDocumentation.ps1
.\scripts\Build-ReleaseDocumentation.ps1 `
    -Version 1.10.0-preview.docs.1 `
    -Tag local `
    -Commit $commit `
    -ReleaseNotesPath docs\1.9.0-release-notes.md `
    -OutputDirectory artifacts\release-docs
.\scripts\Validate-ReleaseDocumentation.ps1 `
    -ArtifactDirectory artifacts\release-docs `
    -ExpectedVersion 1.10.0-preview.docs.1 `
    -ExpectedCommit $commit `
    -ExpectedTag local
```

`-ReleaseNotesPath` is an explicit local-dry-run override. The release workflow does not use it;
real releases must provide `docs/<version>-release-notes.md`. Do not use an older release-note file
for a published release.

Generated ZIP files belong under ignored `artifacts/` and must not be committed.

## Offline HTML and API reference

DocFX is pinned by `.config/dotnet-tools.json`; restore it with `dotnet tool restore`. The tracked
landing page and navigation live under `docs-site/`. The build script creates an isolated temporary
DocFX source tree instead of writing generated `api/` or `_site/` content into the repository.

API metadata is generated from the already-built Release DLLs and their XML documentation for the
seven published library packages. Templates, tests, samples, designer hosts, backend tools, and
the unpublished experimental Android backend are excluded. DocFX's default public/protected API
filter remains enabled.

The build also writes `reference/public-api.txt` and `reference/public-api.json` from the DocFX
managed-reference identifiers. These stable, machine-readable snapshots are intended for future
release-to-release compatibility comparisons; they do not yet enforce a compatibility policy.

The validator checks every local HTML/CSS resource target, rejects root-relative paths and external
CSS/JavaScript/image dependencies, and requires `index.html` plus `api/index.html`. Normal outbound
article links may remain online links. The generated site does not assume a domain root, so the
same site can later be deployed below a version-specific path.

DocFX can emit links for empty intermediate namespace segments even though it creates no page for
those synthetic namespaces. A narrow post-process converts only those missing links in the API
`Namespace` fact to plain text. All other unresolved internal links remain release-blocking errors.

## Sample selection

The sample archive contains:

- `examples/ControlGallery` for controls, layout, rendering, input, themes, shapes, and animations;
- `examples/Explorer` and `examples/Outlaw` as broader Windows application examples;
- `reference/ModernFormsNext.DemoApp` as the clean generated-template reference, not a playground.

During staging, repository `ProjectReference` items are replaced with exact-version
`PackageReference` items. This keeps the downloaded archive independent from the source tree and
prevents examples from silently consuming a later framework build.

`ModernFormsNext.DesignerPlayground` is an internal validation host. The Android smoke test is a
technical backend host. The cross-platform sample depends on the unpublished Android backend and
repository-level scripts. Those projects remain in the tagged source checkout and versioned docs,
but are not copied into a misleading standalone samples bundle.

To add a sample, update `Get-ReleaseSampleSpecs` in
`scripts/ReleaseDocumentation.Common.psm1`, ensure all of its project references map to packages
published at the same version, update this document, and extend the script tests and archive
validator. Do not add a source-checkout-only technical host as a public offline sample.

## Migration guides

Add a guide under `docs/migrations/`, for example:

```text
docs/migrations/1.9.0-to-1.10.0.md
```

All tracked migration guides are included automatically. A release without a migration guide does
not fail; do not create a synthetic guide when there are no known migration steps.

## Security and exclusions

Bundle inputs are allowlisted and selected from Git-tracked paths. Validation rejects `.git`,
`artifacts`, `bin`, `obj`, `.vs`, test/temp directories, `.env`, user/IDE state, signing files,
NuGet/symbol packages, VSIX/APK outputs, path traversal, absolute archive paths, and local user
paths embedded in text files. NuGet packages, `.snupkg` files, VSIX files, and repository internals
remain separate artifacts with separate purposes.

When debugging a failure:

1. run `scripts/tests/Test-ReleaseDocumentation.ps1`;
2. confirm the Release build produced the expected DLL/XML pairs;
3. run the build script without `-RequireTag` and with an explicit local release-note override;
4. inspect DocFX warnings rather than suppressing them;
5. run `Validate-ReleaseDocumentation.ps1 -Verbose` and inspect the named ZIP entry or link;
6. confirm no stale ZIP from another version was substituted.

Never weaken archive validation merely to complete a release. Fix the selected content,
documentation link, metadata, or sample dependency at its source.
