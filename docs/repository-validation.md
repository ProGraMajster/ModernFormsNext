# Repository validation inputs

Repository-level validation must describe the tracked ModernFormsNext source tree, not every file
that happens to exist beneath a checkout. Local `artifacts/` content can contain complete Git
worktrees, release copies, package output, and files created by unrelated tasks; treating those
paths as source produces duplicate projects and false version failures.

## Source selection

`ModernFormsNext.Tests` resolves the repository root by walking upward from the test output
directory until both `Directory.Build.props` and `ModernFormsNext.slnx` are present. It never uses
the process working directory as the root.

When the root contains `.git` as either a directory or a worktree file, repository project
enumeration asks `git ls-files` for tracked inputs. Git does not require network access for this
operation. If repository metadata or the Git executable is unavailable, as in a source archive,
validation falls back to a bounded filesystem traversal.

The fallback excludes these exact directory names, case-insensitively:

- `.git`, `.vs`, `.idea`, `.codex`, `.nuget`, and `.cache`;
- `artifacts`, `bin`, and `obj`;
- `TestResult`, `TestResults`, `packages`, and `node_modules`;
- `BenchmarkDotNet.Artifacts`, `Generated Files`, `GeneratedArtifacts`, `AppPackages`, and
  `BundleArtifacts`;
- local validation outputs `.codex-build`, `.codex-pack`, and `.codex-pack-api`;
- DocFX output `_site`.

Names are compared as complete path segments. A source directory such as
`ArtifactsDocumentation`, `my-artifacts-source`, `BinaryTools`, or `ObjectModel` is not excluded.
Add a directory to the central list only when it is generated or user-specific in every location
where that segment can occur. Prefer a narrower caller-specific scope when that is not true.

## Nested repositories and path safety

Filesystem fallback prunes a child directory when it contains `.git` as either a directory or a
file. The latter is the normal shape of a linked Git worktree. Generated directories are pruned
before nested-worktree heuristics, so an `artifacts/` tree is never recursively inspected.

Directory and file reparse points are not followed. This prevents symlink or Windows junction
cycles and prevents repository validation from reading outside the normalized root. Git output is
also normalized and rejected if it is absolute, contains `.` or `..` segments, or resolves outside
the root.

Results and diagnostics use repository-relative forward-slash paths, are ordered with ordinal
comparison, and never embed a local checkout path. Validation failures identify whether Git tracked
files or the filesystem fallback supplied the inputs and list included and explicitly excluded
paths.

## Choosing traversal semantics

Use tracked Git files for repository structure, version, package metadata, and release-input tests.
Use the safe fallback only when the same test is required to work from a source archive without
Git. Do not use either mechanism for a caller that intentionally validates an explicit artifact
directory, package archive, generated documentation staging tree, or user-selected filesystem
location.

The release documentation scripts already select tracked documentation and samples with
`git ls-files`; their later recursive operations are limited to explicit staging or archive
directories. Package and Android scripts likewise enumerate explicit output directories. These
callers intentionally do not use repository source traversal.

## Writing repository-level tests

1. Resolve the root through `RepositoryFileEnumerator.FindRepositoryRoot`.
2. Enumerate the narrowest file type needed and keep allowlists explicit.
3. Test both Git-tracked input and the no-Git fallback.
4. Add fixtures for generated paths, nested `.git` directories and files, similar non-generated
   names, separator normalization, and unsafe paths.
5. Confirm a real source-tree inconsistency is still included and fails validation.
6. Keep output deterministic and repository-relative; never print user-specific absolute paths.

Clean external exact-SHA worktrees remain recommended for final release provenance. They are a
release-safety measure, not a workaround required for correctness of repository enumeration.
Sequential `-m:1 /p:UseSharedCompilation=false` builds remain required where MicroCom projects share
intermediate output paths; repository filtering does not hide or solve genuine concurrent writers.
