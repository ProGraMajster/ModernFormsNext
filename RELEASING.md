# Releasing ModernFormsNext

This repository creates GitHub Releases from version tags, not from ordinary pushes to `master`.

The regular `.NET` workflow validates pushes and pull requests to `master`. The `Release` workflow in `.github/workflows/release.yml` runs only after a Git tag matching `v*.*.*` is pushed, for example `v1.3.0`.

The current `Release` workflow also publishes `.nupkg` packages to NuGet after creating the GitHub Release. Pushing a release tag is therefore both the GitHub Release trigger and the NuGet publish trigger.

## Version numbers

NuGet package versions use SemVer without a `v` prefix:

```xml
<ModernFormsNextPackageVersion>1.3.0</ModernFormsNextPackageVersion>
```

GitHub tags use the same version with a `v` prefix:

```text
v1.3.0
```

Do not put the `v` prefix in `.csproj`, `Directory.Build.props`, template package references, or NuGet metadata. The `v` prefix belongs only to the Git tag.

## Choosing the next version

Use SemVer:

- `1.0.0 -> 1.0.1` for a bug fix, CI fix, documentation fix, or other small compatible change.
- `1.0.0 -> 1.1.0` for a new backward-compatible feature or public API.
- `1.0.0 -> 2.0.0` for a breaking public API change.
- `1.1.0 -> 1.2.0-preview.1` for a preview or test release.

NuGet package versions cannot be published again. If `1.0.0` has already been published, the next fix release must use a new version such as `1.0.1`. Do not push a release tag until the package version is final and ready to publish.

## Release workflow

1. Update the package version in the project metadata. In this repository the shared package version is stored in `Directory.Build.props` as `ModernFormsNextPackageVersion`.
2. Update template package references and documentation if they mention the released package version.
3. Restore, build, and pack locally if needed.
4. Commit the version change and push it to `master`.
5. Wait until the regular `.NET` workflow is green for that commit.
6. Create a Git tag in the format `vX.Y.Z`, for example `v1.0.1`.
7. Push the tag.
8. The `Release` workflow starts only after the tag is pushed.
9. The `Release` workflow builds the solution in Release mode, runs `dotnet pack`, creates a GitHub Release, uploads `.nupkg` and `.snupkg` assets, and publishes `.nupkg` packages to NuGet.

## Commands

Example for a patch release from `1.0.0` to `1.0.1`:

```powershell
git add .
git commit -m "chore: release 1.0.1"
git push

git tag v1.0.1
git push origin v1.0.1
```

The tag must match the `v*.*.*` pattern. Examples of valid release tags:

```text
v1.0.1
v1.2.0
v2.0.0
```

Examples that should not be used for a release:

```text
1.0.1
release-1.0.1
v1.0
```

## Diagnostics

List local tags:

```powershell
git tag
```

List tags that exist on GitHub:

```powershell
git ls-remote --tags origin
```

If the release workflow did not start, verify that the tag was pushed to `origin` and that it matches `v*.*.*`.
