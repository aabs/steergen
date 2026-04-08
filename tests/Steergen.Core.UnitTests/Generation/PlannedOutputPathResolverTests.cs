using Steergen.Core.Generation;

namespace Steergen.Core.UnitTests.Generation;

public sealed class PlannedOutputPathResolverTests
{
    [Fact]
    public void Resolve_RelativePlanPath_PreservesNestedLayoutUnderOutputBase()
    {
        var outputBase = Path.Combine(Path.GetTempPath(), "planned-output-base");

        var result = PlannedOutputPathResolver.Resolve(
            Path.Combine(".kiro", "steering", "accessibility-standards.md"),
            outputBase,
            globalRoot: null,
            projectRoot: null);

        Assert.Equal(
            Path.Combine(outputBase, ".kiro", "steering", "accessibility-standards.md"),
            result);
    }

    [Fact]
    public void Resolve_AbsolutePathUnderGlobalRoot_StripsOnlyTheConfiguredRoot()
    {
        var globalRoot = Path.Combine(Path.GetTempPath(), $"global-{Guid.NewGuid():N}");
        var outputBase = Path.Combine(Path.GetTempPath(), $"output-{Guid.NewGuid():N}");
        var planPath = Path.Combine(globalRoot, ".kiro", "steering", "accessibility-standards.md");

        var result = PlannedOutputPathResolver.Resolve(planPath, outputBase, globalRoot, projectRoot: null);

        Assert.Equal(
            Path.Combine(outputBase, ".kiro", "steering", "accessibility-standards.md"),
            result);
    }

    [Fact]
    public void Resolve_AbsolutePathOutsideKnownRoots_FallsBackToFileNameOnly()
    {
        var globalRoot = Path.Combine(Path.GetTempPath(), $"global-{Guid.NewGuid():N}");
        var outputBase = Path.Combine(Path.GetTempPath(), $"output-{Guid.NewGuid():N}");
        var planPath = Path.Combine(Path.GetTempPath(), $"external-{Guid.NewGuid():N}", "nested", "accessibility-standards.md");

        var result = PlannedOutputPathResolver.Resolve(planPath, outputBase, globalRoot, projectRoot: null);

        Assert.Equal(Path.Combine(outputBase, "accessibility-standards.md"), result);
    }

    [Fact]
    public void Resolve_PathWithSharedPrefixButOutsideRoot_DoesNotStripByPrefixOnly()
    {
        var outputBase = Path.Combine(Path.GetTempPath(), $"output-{Guid.NewGuid():N}");
        var root = Path.Combine(Path.GetTempPath(), $"root-{Guid.NewGuid():N}");
        var siblingWithSharedPrefix = $"{root}-shadow";
        var planPath = Path.Combine(siblingWithSharedPrefix, ".kiro", "steering", "accessibility-standards.md");

        var result = PlannedOutputPathResolver.Resolve(planPath, outputBase, root, projectRoot: null);

        Assert.Equal(Path.Combine(outputBase, "accessibility-standards.md"), result);
    }

    [Fact]
    public void Resolve_AbsolutePathUnderProjectRoot_StripsOnlyTheConfiguredProjectRoot()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"project-{Guid.NewGuid():N}");
        var outputBase = Path.Combine(Path.GetTempPath(), $"output-{Guid.NewGuid():N}");
        var planPath = Path.Combine(projectRoot, ".speckit", "memory", "constitution.md");

        var result = PlannedOutputPathResolver.Resolve(planPath, outputBase, globalRoot: null, projectRoot);

        Assert.Equal(
            Path.Combine(outputBase, ".speckit", "memory", "constitution.md"),
            result);
    }

    /// <summary>
    /// Regression test for the case where both projectRoot and the plan path are relative
    /// (e.g. projectRoot: "docs/steering" in steergen.config.yaml), causing the plan path
    /// to be "docs/steering/.kiro/steering/file.md". The resolver must strip the root prefix
    /// and rebase under outputPath rather than carrying the prefix into the output tree.
    /// </summary>
    [Fact]
    public void Resolve_RelativePlanPathContainingRelativeProjectRootPrefix_StripsRootAndRebasesUnderOutputBase()
    {
        var outputBase = Path.Combine(Path.GetTempPath(), $"output-{Guid.NewGuid():N}");
        var relativeProjectRoot = Path.Combine("docs", "steering");
        // Plan path as produced when ${projectRoot} = "docs/steering" is substituted in
        // a layout destination of "${projectRoot}/.kiro/steering/${inputFileStem}.md".
        var relativePlanPath = Path.Combine("docs", "steering", ".kiro", "steering", "architecture.md");

        // Path.GetFullPath inside TryResolveRelativeToRoot uses the process working directory.
        // A temp sub-directory that actually exists on disk is required for GetFullPath to work
        // correctly on all platforms.
        var cwd = Directory.CreateTempSubdirectory("resolver-cwd-").FullName;
        try
        {
            var savedDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(cwd);

                var result = PlannedOutputPathResolver.Resolve(
                    relativePlanPath,
                    outputBase,
                    globalRoot: null,
                    projectRoot: relativeProjectRoot);

                Assert.Equal(
                    Path.Combine(outputBase, ".kiro", "steering", "architecture.md"),
                    result);
            }
            finally
            {
                Directory.SetCurrentDirectory(savedDir);
            }
        }
        finally
        {
            if (Directory.Exists(cwd)) Directory.Delete(cwd, recursive: true);
        }
    }
}