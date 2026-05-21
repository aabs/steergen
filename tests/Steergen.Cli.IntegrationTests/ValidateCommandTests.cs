using Steergen.Cli.Commands;
using Steergen.Core.Configuration;
using Steergen.Core.Model;

namespace Steergen.Cli.IntegrationTests;

[Collection("CliOutput")]

public sealed class ValidateCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    // ── Happy-path: valid corpus produces exit code 0 ──────────────────────

    [Fact]
    public async Task Validate_ValidCorpus_ReturnsExitCode0()
    {
        var result = await ValidateCommand.RunAsync(
            globalRoot: Path.Combine(FixturesRoot, "global"),
            projectRoot: Path.Combine(FixturesRoot, "project"),
            quiet: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Validate_CommandAutoDiscoversConfigFromCurrentDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            await WriteConfigAsync(
                dir,
                Path.Combine(FixturesRoot, "global"),
                Path.Combine(FixturesRoot, "project"));

            using var scope = new CurrentDirectoryScope(dir);
            var result = await ValidateCommand.Create().Parse("--quiet").InvokeAsync();

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Schema errors: missing document ID ─────────────────────────────────

    [Fact]
    public async Task Validate_DocumentMissingId_ReturnsExitCode1()
    {
        var dir = CreateTempDir();
        try
        {
            // Document has no frontmatter → no id
            await File.WriteAllTextAsync(Path.Combine(dir, "bad.md"),
                """
                :::rule id="R001" severity="info" domain="core"
                Some rule.
                :::
                """);

            var result = await ValidateCommand.RunAsync(
                globalRoot: dir, projectRoot: null, quiet: true);

            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Severity validation ─────────────────────────────────────────────────

    [Fact]
    public async Task Validate_LegacySeverityAttribute_IsIgnoredReturnsExitCode0()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "bad-sev.md"),
                """
                ---
                id: doc-bad-sev
                ---
                :::rule id="R001" severity="critical" category="core"
                A rule with a legacy severity attribute that is now silently ignored.
                :::
                """);

            var result = await ValidateCommand.RunAsync(
                globalRoot: dir, projectRoot: null, quiet: true);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Duplicate rule IDs across documents ────────────────────────────────

    [Fact]
    public async Task Validate_DuplicateRuleIds_ReturnsExitCode1()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "doc-a.md"),
                """
                ---
                id: doc-a
                ---
                :::rule id="DUP-001" severity="info" domain="core"
                First occurrence.
                :::
                """);

            await File.WriteAllTextAsync(Path.Combine(dir, "doc-b.md"),
                """
                ---
                id: doc-b
                ---
                :::rule id="DUP-001" severity="info" domain="core"
                Duplicate occurrence.
                :::
                """);

            var result = await ValidateCommand.RunAsync(
                globalRoot: dir, projectRoot: null, quiet: true);

            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Supersedes warning: unknown target rule ─────────────────────────────

    [Fact]
    public async Task Validate_SupersedesUnknownRule_ProducesWarningNotError()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "doc.md"),
                """
                ---
                id: doc-supersedes
                ---
                :::rule id="NEW-001" severity="info" domain="core" supersedes="OLD-999"
                Replaces a rule that does not exist.
                :::
                """);

            // Should still succeed (warnings do not fail validation)
            var result = await ValidateCommand.RunAsync(
                globalRoot: dir, projectRoot: null, quiet: true);

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Missing global/project directory returns exit code 2 ───────────────

    [Fact]
    public async Task Validate_NonexistentGlobalDir_ReturnsExitCode2()
    {
        var result = await ValidateCommand.RunAsync(
            globalRoot: "/nonexistent/path/that/does/not/exist",
            projectRoot: null,
            quiet: true);

        Assert.Equal(2, result);
    }

    // ── Mixed global + project paths ───────────────────────────────────────

    [Fact]
    public async Task Validate_GlobalAndProjectPaths_BothValidated()
    {
        var globalDir = CreateTempDir();
        var projectDir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(globalDir, "global.md"),
                """
                ---
                id: global-doc
                ---
                :::rule id="G-001" severity="info" domain="core"
                A global rule.
                :::
                """);

            // Project doc is invalid (missing id)
            await File.WriteAllTextAsync(Path.Combine(projectDir, "project.md"),
                """
                :::rule id="P-001" severity="info" domain="core"
                A project rule with no document id.
                :::
                """);

            var result = await ValidateCommand.RunAsync(
                globalRoot: globalDir, projectRoot: projectDir, quiet: true);

            Assert.Equal(1, result);
        }
        finally
        {
            Directory.Delete(globalDir, recursive: true);
            Directory.Delete(projectDir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"validate-integ-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WriteConfigAsync(string dir, string globalRoot, string projectRoot)
    {
        var writer = new SteergenConfigWriter();
        await writer.WriteAsync(
            Path.Combine(dir, "steergen.config.yaml"),
            new SteeringConfiguration
            {
                ProjectRoot = projectRoot,
            });
    }

    // ── Template pack validation: valid templates produce exit code 0 ──────

    [Fact]
    public async Task Validate_ValidTemplatePack_ReturnsExitCode0()
    {
        var dir = CreateTempDir();
        var packDir = Path.Combine(dir, "templates");
        Directory.CreateDirectory(Path.Combine(packDir, "kiro"));
        try
        {
            // Write a valid Scriban template
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "{{ rules }}");

            // Write config pointing to the local template pack
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(
                Path.Combine(dir, "steergen.config.yaml"),
                new SteeringConfiguration
                {
                    RegisteredTargets = ["kiro"],
                    TemplatePack = new TemplatePackConfig { LocalPath = packDir },
                });

            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: true,
                configPath: Path.Combine(dir, "steergen.config.yaml"));

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Template pack validation: syntax errors produce exit code 1 ────────

    [Fact]
    public async Task Validate_TemplatePackWithSyntaxErrors_ReturnsExitCode1()
    {
        var dir = CreateTempDir();
        var packDir = Path.Combine(dir, "templates");
        Directory.CreateDirectory(Path.Combine(packDir, "kiro"));
        try
        {
            // Write an invalid Scriban template (unclosed if block)
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "document.scriban"),
                "{{ if true }}content without end");

            // Write config pointing to the local template pack
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(
                Path.Combine(dir, "steergen.config.yaml"),
                new SteeringConfiguration
                {
                    RegisteredTargets = ["kiro"],
                    TemplatePack = new TemplatePackConfig { LocalPath = packDir },
                });

            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: true,
                configPath: Path.Combine(dir, "steergen.config.yaml"));

            Assert.Equal(1, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Template pack validation: unknown template name produces warning ───

    [Fact]
    public async Task Validate_TemplatePackWithUnknownTemplateName_ProducesWarningNotError()
    {
        var dir = CreateTempDir();
        var packDir = Path.Combine(dir, "templates");
        Directory.CreateDirectory(Path.Combine(packDir, "kiro"));
        try
        {
            // Write a valid template with an unknown name for the kiro target
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "kiro", "unknown-template.scriban"),
                "plain text content");

            // Write config pointing to the local template pack
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(
                Path.Combine(dir, "steergen.config.yaml"),
                new SteeringConfiguration
                {
                    RegisteredTargets = ["kiro"],
                    TemplatePack = new TemplatePackConfig { LocalPath = packDir },
                });

            // Warnings do not cause exit code 1
            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: false,
                configPath: Path.Combine(dir, "steergen.config.yaml"));

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Template pack validation: unregistered target produces warning ─────

    [Fact]
    public async Task Validate_TemplatePackWithUnregisteredTarget_ProducesWarning()
    {
        var dir = CreateTempDir();
        var packDir = Path.Combine(dir, "templates");
        Directory.CreateDirectory(Path.Combine(packDir, "unknown-target"));
        try
        {
            // Write a valid template for an unregistered target
            await File.WriteAllTextAsync(
                Path.Combine(packDir, "unknown-target", "document.scriban"),
                "plain text content");

            // Write config with only "kiro" registered (not "unknown-target")
            var writer = new SteergenConfigWriter();
            await writer.WriteAsync(
                Path.Combine(dir, "steergen.config.yaml"),
                new SteeringConfiguration
                {
                    RegisteredTargets = ["kiro"],
                    TemplatePack = new TemplatePackConfig { LocalPath = packDir },
                });

            // Warnings do not cause exit code 1
            var result = await ValidateCommand.RunAsync(
                globalRoot: null,
                projectRoot: null,
                quiet: false,
                configPath: Path.Combine(dir, "steergen.config.yaml"));

            Assert.Equal(0, result);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
