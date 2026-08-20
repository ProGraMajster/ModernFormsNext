using System.Xml.Linq;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class RepositoryFileEnumeratorTests
{
    [Fact]
    public void FindsRepositoryRootFromNestedTestOutputWithoutUsingCurrentDirectory()
    {
        using var fixture = new RepositoryFixture();
        string nested = fixture.CreateDirectory("tests/bin/Debug/net10.0");

        Assert.Equal(fixture.Root, RepositoryFileEnumerator.FindRepositoryRoot(nested));
    }

    [Theory]
    [InlineData("artifacts/old/App.csproj")]
    [InlineData("ARTIFACTS\\old\\App.csproj")]
    [InlineData("src/bin/App.csproj")]
    [InlineData("src\\OBJ\\generated.props")]
    [InlineData("src/TestResults/result.trx")]
    [InlineData("src/packages/cache.nupkg")]
    [InlineData("src/.nuget/packages/cache.nupkg")]
    [InlineData("src/.cache/generated/App.csproj")]
    [InlineData("src/.codex/local/App.csproj")]
    [InlineData("src/node_modules/package.json")]
    [InlineData("external/.git/config")]
    public void GeneratedDirectoryMatchingUsesExactCaseInsensitiveSegments(string path)
        => Assert.True(RepositoryFileEnumerator.ContainsGeneratedDirectorySegment(path));

    [Theory]
    [InlineData("ArtifactsDocumentation/App.csproj")]
    [InlineData("my-artifacts-source/App.csproj")]
    [InlineData("BinaryTools/App.csproj")]
    [InlineData("ObjectModel/App.csproj")]
    public void SimilarDirectoryNamesRemainRepositorySource(string path)
        => Assert.False(RepositoryFileEnumerator.ContainsGeneratedDirectorySegment(path));

    [Fact]
    public void FilesystemFallbackExcludesGeneratedTreesAndKeepsStableSourceOrder()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/Zeta/Zeta.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");
        fixture.WriteProject("src/Alpha/Alpha.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");
        fixture.WriteProject("ArtifactsDocumentation/Docs.csproj", isPackable: false, version: "1.0.0");
        fixture.WriteProject("my-artifacts-source/Source.csproj", isPackable: false, version: "1.0.0");
        fixture.WriteProject("artifacts/old/Old.csproj", isPackable: true, version: "1.9.0");
        fixture.WriteProject("bin/Binary.csproj", isPackable: true, version: "1.9.0");
        fixture.WriteFile("obj/metadata.props", "<Project />");

        RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);

        Assert.Equal(RepositoryFileSource.FileSystemFallback, result.Source);
        Assert.Equal(
            [
                "ArtifactsDocumentation/Docs.csproj",
                "my-artifacts-source/Source.csproj",
                "src/Alpha/Alpha.csproj",
                "src/Zeta/Zeta.csproj"
            ],
            result.Files);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "artifacts");
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "bin");
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "obj");
    }

    [Fact]
    public void SourceArchiveWithoutGitUsesFilesystemFallback()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/App.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");

        RepositoryFileEnumeration result = RepositoryFileEnumerator.EnumerateFiles(fixture.Root, IsProject);

        Assert.Equal(RepositoryFileSource.FileSystemFallback, result.Source);
        Assert.Equal(["src/App.csproj"], result.Files);
    }

    [Fact]
    public void UnusableWorktreeMetadataFallsBackWithoutMakingGitMandatory()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteFile(".git", "gitdir: missing-worktree-metadata");
        fixture.WriteProject("src/App.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");

        RepositoryFileEnumeration result = RepositoryFileEnumerator.EnumerateFiles(fixture.Root, IsProject);

        Assert.Equal(RepositoryFileSource.FileSystemFallback, result.Source);
        Assert.Equal(["src/App.csproj"], result.Files);
    }

    [Fact]
    public void FilesystemFallbackPrunesArtifactsContainingACompleteNestedWorktree()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/App.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");
        fixture.WriteFile("artifacts/issue-83/.git", "gitdir: ../../.git/worktrees/issue-83");
        fixture.WriteFile("artifacts/issue-83/Directory.Build.props", "<Project><PropertyGroup><ModernFormsNextPackageVersion>1.9.0</ModernFormsNextPackageVersion></PropertyGroup></Project>");
        fixture.WriteFile("artifacts/issue-83/ModernFormsNext.slnx", "<Solution />");
        fixture.WriteProject("artifacts/issue-83/Old.csproj", isPackable: true, version: "1.9.0");

        RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);

        Assert.Equal(["src/App.csproj"], result.Files);
    }

    [Fact]
    public void FilesystemFallbackPrunesNestedGitDirectoryAndGitFile()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteFile("external-repository/.git/config", "[core]");
        fixture.WriteProject("external-repository/External.csproj", isPackable: true, version: "1.9.0");
        fixture.WriteFile("linked-worktree/.git", "gitdir: ../.git/worktrees/linked");
        fixture.WriteProject("linked-worktree/Linked.csproj", isPackable: true, version: "1.9.0");
        fixture.WriteProject("source/Source.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");

        RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);

        Assert.Equal(["source/Source.csproj"], result.Files);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "external-repository" && exclusion.Reason.Contains("nested Git", StringComparison.Ordinal));
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "linked-worktree" && exclusion.Reason.Contains("nested Git", StringComparison.Ordinal));
    }

    [Fact]
    public void GitTrackedFilesArePreferredAndGeneratedSegmentsRemainExcluded()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/App.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");
        fixture.WriteProject("artifacts/Old.csproj", isPackable: true, version: "1.9.0");

        RepositoryFileEnumeration result = RepositoryFileEnumerator.EnumerateFiles(
            fixture.Root,
            IsProject,
            _ => ["src/App.csproj", "artifacts/Old.csproj"]);

        Assert.Equal(RepositoryFileSource.GitTrackedFiles, result.Source);
        Assert.Equal(["src/App.csproj"], result.Files);
        Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "artifacts/Old.csproj");
    }

    [Fact]
    public void GitTrackedInputNormalizesWindowsAndUnixSeparators()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/Unix.csproj", isPackable: false, version: "1.0.0");
        fixture.WriteProject("src/Windows.csproj", isPackable: false, version: "1.0.0");

        RepositoryFileEnumeration result = RepositoryFileEnumerator.EnumerateFiles(
            fixture.Root,
            IsProject,
            _ => ["src/Unix.csproj", "src\\Windows.csproj"]);

        Assert.Equal(["src/Unix.csproj", "src/Windows.csproj"], result.Files);
    }

    [Fact]
    public void UnsafeTrackedPathCannotEscapeRepositoryRoot()
    {
        using var fixture = new RepositoryFixture();

        Assert.Throws<InvalidDataException>(() => RepositoryFileEnumerator.EnumerateFiles(
            fixture.Root,
            IsProject,
            _ => ["../outside.csproj"]));
    }

    [Fact]
    public void FilesystemFallbackStillIncludesARealSourceProjectWithAnInvalidVersion()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/Broken.csproj", isPackable: true, version: "1.9.0");

        RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);
        string projectPath = Assert.Single(result.Files);
        XDocument project = XDocument.Load(IOPath.Combine(fixture.Root, projectPath.Replace('/', IOPath.DirectorySeparatorChar)));

        Assert.Equal("1.9.0", project.Descendants("Version").Single().Value);
        Assert.NotEqual("$(ModernFormsNextPackageVersion)", project.Descendants("Version").Single().Value);
    }

    [Fact]
    public void ReparsePointAttributeIsRecognizedWithoutFollowingTheTarget()
        => Assert.True(RepositoryFileEnumerator.IsReparsePoint(FileAttributes.Directory | FileAttributes.ReparsePoint));

    [Fact]
    public void FilesystemFallbackDoesNotFollowDirectorySymlinksWhenSupported()
    {
        using var fixture = new RepositoryFixture();
        string outside = IOPath.Combine(IOPath.GetTempPath(), $"ModernFormsNext.RepositoryOutside.{Guid.NewGuid():N}");
        string link = IOPath.Combine(fixture.Root, "linked-source");
        Directory.CreateDirectory(outside);
        File.WriteAllText(IOPath.Combine(outside, "Outside.csproj"), "<Project><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                return;
            }

            RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);

            Assert.Empty(result.Files);
            Assert.Contains(result.Exclusions, exclusion => exclusion.Path == "linked-source" && exclusion.Reason == "reparse point");
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            if (Directory.Exists(outside))
                Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void DiagnosticsContainOnlyRepositoryRelativePaths()
    {
        using var fixture = new RepositoryFixture();
        fixture.WriteProject("src/App.csproj", isPackable: true, version: "$(ModernFormsNextPackageVersion)");
        RepositoryFileEnumeration result = EnumerateFallback(fixture.Root);

        string diagnostics = result.FormatDiagnostics(["src/Expected.csproj"]);

        Assert.Contains("src/App.csproj", diagnostics, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, diagnostics, StringComparison.OrdinalIgnoreCase);
    }

    private static RepositoryFileEnumeration EnumerateFallback(string root)
        => RepositoryFileEnumerator.EnumerateFiles(root, IsProject, _ => null);

    private static bool IsProject(string path)
        => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

    private sealed class RepositoryFixture : IDisposable
    {
        public RepositoryFixture()
        {
            Root = IOPath.Combine(IOPath.GetTempPath(), $"ModernFormsNext.RepositoryTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            WriteFile("Directory.Build.props", "<Project />");
            WriteFile("ModernFormsNext.slnx", "<Solution />");
        }

        public string Root { get; }

        public string CreateDirectory(string relativePath)
        {
            string path = GetPath(relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public void WriteProject(string relativePath, bool isPackable, string version)
            => WriteFile(
                relativePath,
                $"<Project><PropertyGroup><IsPackable>{isPackable.ToString().ToLowerInvariant()}</IsPackable><Version>{version}</Version></PropertyGroup></Project>");

        public void WriteFile(string relativePath, string content)
        {
            string path = GetPath(relativePath);
            Directory.CreateDirectory(IOPath.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }

        private string GetPath(string relativePath)
            => IOPath.Combine(Root, relativePath.Replace('/', IOPath.DirectorySeparatorChar));
    }
}
