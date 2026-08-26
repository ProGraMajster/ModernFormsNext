using System.Xml.Linq;
using Xunit;

namespace ModernFormsNext.Tests;

public sealed class VisualStudioDesignerProjectMetadataTests
{
    [Fact]
    public void RuntimePackageShipsConventionBasedDesignerCompanionNesting()
    {
        var project = LoadXml("ModernFormsNext/ModernFormsNext.csproj");
        var packedTarget = Assert.Single(
            project.Descendants("None"),
            element => string.Equals(
                element.Attribute("Include")?.Value,
                "buildTransitive\\ModernFormsNext.targets",
                StringComparison.Ordinal));
        Assert.Equal("true", packedTarget.Attribute("Pack")?.Value);
        Assert.Equal("buildTransitive\\ModernFormsNext.targets", packedTarget.Attribute("PackagePath")?.Value);

        var targets = LoadXml("ModernFormsNext/buildTransitive/ModernFormsNext.targets");
        var designerCode = Assert.Single(
            targets.Descendants("Compile"),
            element => string.Equals(element.Attribute("Update")?.Value, "**/*.Designer.cs", StringComparison.Ordinal));
        var designDocument = Assert.Single(
            targets.Descendants("None"),
            element => string.Equals(element.Attribute("Update")?.Value, "**/*.mfdesign", StringComparison.Ordinal));

        Assert.Null(designerCode.Attribute("Condition"));
        var designerCodeDependency = Assert.Single(designerCode.Elements("DependentUpon"));
        Assert.Equal(
            "'%(DependentUpon)' == '' and Exists('$([System.String]::Copy(\"%(FullPath)\").Replace(\".Designer.cs\", \".mfdesign\"))')",
            designerCodeDependency.Attribute("Condition")?.Value);
        Assert.Equal(
            "$([System.String]::Copy('%(Filename)').Replace('.Designer', '')).cs",
            designerCodeDependency.Value);
        Assert.Null(designDocument.Attribute("Condition"));
        var designDocumentDependency = Assert.Single(designDocument.Elements("DependentUpon"));
        Assert.Equal(
            "'%(DependentUpon)' == '' and Exists('$([System.IO.Path]::ChangeExtension(\"%(FullPath)\", \".cs\"))')",
            designDocumentDependency.Attribute("Condition")?.Value);
        Assert.Equal("%(Filename).cs", designDocumentDependency.Value);
    }

    [Fact]
    public void SharedVisualStudioContractUsesProjectReferencesWithoutUiOrLinkedSourceDependencies()
    {
        var sharedProject = LoadXml(
            "ModernFormsNext.VisualStudioExtension.Shared/ModernFormsNext.VisualStudioExtension.Shared.csproj");
        Assert.Equal("netstandard2.0", Assert.Single(sharedProject.Descendants("TargetFramework")).Value);
        Assert.Empty(sharedProject.Descendants("PackageReference"));
        Assert.Empty(sharedProject.Descendants("ProjectReference"));
        Assert.Empty(sharedProject.Descendants("Reference"));
        Assert.Empty(sharedProject.Descendants("UseWindowsForms"));

        foreach (var projectPath in new[]
                 {
                     "ModernFormsNext.VisualStudioExtension/ModernFormsNext.VisualStudioExtension.csproj",
                     "ModernFormsNext.VisualStudioExtension.Vsix/ModernFormsNext.VisualStudioExtension.Vsix.csproj"
                 })
        {
            var project = LoadXml(projectPath);
            Assert.Contains(
                project.Descendants("ProjectReference"),
                element => element.Attribute("Include")?.Value
                    == "..\\ModernFormsNext.VisualStudioExtension.Shared\\ModernFormsNext.VisualStudioExtension.Shared.csproj");
            Assert.DoesNotContain(
                project.Descendants("Compile"),
                element => element.Attribute("Include")?.Value?.Contains(
                    "VisualStudioExtension.Shared",
                    StringComparison.OrdinalIgnoreCase) == true);
        }

        var sharedSource = File.ReadAllText(GetRepositoryPath(
            "ModernFormsNext.VisualStudioExtension.Shared/VisualStudioDesignerCommandStatus.cs"));
        Assert.DoesNotContain("System.Windows.Forms", sharedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Drawing", sharedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.VisualStudio", sharedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ModernFormsNext.Designer", sharedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ModernFormsNextForm", "ModernFormsNextForm")]
    [InlineData("ModernFormsNextUserControl", "ModernFormsNextUserControl")]
    public void VsixItemTemplatesCreateTheCompleteDesignableFileSet(string templateFolder, string expectedSubtype)
    {
        var template = LoadXml(
            $"ModernFormsNext.VisualStudioExtension.Vsix/ItemTemplates/CSharp/ModernFormsNext/{templateFolder}/{templateFolder}.vstemplate");
        var projectItems = template.Descendants().Where(element => element.Name.LocalName == "ProjectItem").ToArray();

        Assert.Equal(3, projectItems.Length);
        Assert.Contains(projectItems, element => element.Attribute("TargetFileName")?.Value == "$fileinputname$.cs"
            && element.Attribute("SubType")?.Value == expectedSubtype);
        Assert.Contains(projectItems, element => element.Attribute("TargetFileName")?.Value == "$fileinputname$.Designer.cs");
        Assert.Contains(projectItems, element => element.Attribute("TargetFileName")?.Value == "$fileinputname$.mfdesign");
    }

    private static XDocument LoadXml(string relativePath)
        => XDocument.Load(GetRepositoryPath(relativePath));

    private static string GetRepositoryPath(string relativePath)
        => IOPath.Combine(
            RepositoryFileEnumerator.FindRepositoryRoot(),
            relativePath.Replace('/', IOPath.DirectorySeparatorChar));
}
