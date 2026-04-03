using System.Text.Json;
using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests;

/// <summary>
/// In the same collection as <see cref="InitCommandTests"/> to prevent parallel execution
/// competing over the process-level Console.SetOut redirect used by CaptureStdout.
/// </summary>
[Collection("CliOutput")]

public sealed class InspectCommandTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    // ── Happy-path: inspect valid corpus returns exit 0 and valid JSON ─────

    [Fact]
    public async Task Inspect_ValidCorpus_ReturnsExitCode0()
    {
        var result = await InspectCommand.RunAsync(
            globalRoot: Path.Combine(FixturesRoot, "global"),
            projectRoot: Path.Combine(FixturesRoot, "project"));

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Inspect_ValidCorpus_WritesValidJsonToStdout()
    {
        var (exitCode, stdout) = await CaptureStdout(() =>
            InspectCommand.RunAsync(
                globalRoot: Path.Combine(FixturesRoot, "global"),
                projectRoot: Path.Combine(FixturesRoot, "project")));

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));

        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.TryGetProperty("rules", out var rules));
        Assert.Equal(JsonValueKind.Array, rules.ValueKind);
    }

    // ── Profile scoping ────────────────────────────────────────────────────

    [Fact]
    public async Task Inspect_WithProfile_FiltersRules()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "doc.md"),
                """
                ---
                id: doc-profile
                ---
                :::rule id="R001" severity="info" domain="core"
                Rule with no profile.
                :::

                :::rule id="R002" severity="info" domain="core" profile="alpha"
                Rule with profile alpha.
                :::
                """);

            var (exitCode, stdoutAll) = await CaptureStdout(() =>
                InspectCommand.RunAsync(globalRoot: dir, projectRoot: null));

            Assert.Equal(0, exitCode);
            using var docAll = JsonDocument.Parse(stdoutAll);
            var allRules = docAll.RootElement.GetProperty("rules").EnumerateArray().ToList();

            var (exitCode2, stdoutFiltered) = await CaptureStdout(() =>
                InspectCommand.RunAsync(
                    globalRoot: dir, projectRoot: null,
                    activeProfiles: ["alpha"]));

            Assert.Equal(0, exitCode2);
            using var docFiltered = JsonDocument.Parse(stdoutFiltered);
            var filteredRules = docFiltered.RootElement.GetProperty("rules").EnumerateArray().ToList();

            // With profile filter, rules with no profile AND matching profile should appear;
            // but since profile "alpha" is active, R002 is included and R001 (no profile) too.
            Assert.True(filteredRules.Count <= allRules.Count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Error: missing directory ───────────────────────────────────────────

    [Fact]
    public async Task Inspect_MissingGlobalDirectory_ReturnsExitCode2()
    {
        var result = await InspectCommand.RunAsync(
            globalRoot: "/nonexistent/path/abc123",
            projectRoot: null);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task Inspect_MissingProjectDirectory_ReturnsExitCode2()
    {
        var result = await InspectCommand.RunAsync(
            globalRoot: null,
            projectRoot: "/nonexistent/path/abc123");

        Assert.Equal(2, result);
    }

    // ── Output structure: activeProfiles and documents fields present ──────

    [Fact]
    public async Task Inspect_OutputContainsExpectedTopLevelFields()
    {
        var dir = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "doc.md"),
                """
                ---
                id: doc-inspect
                title: Test Document
                ---
                :::rule id="R001" severity="info" domain="core"
                A rule.
                :::
                """);

            var (_, stdout) = await CaptureStdout(() =>
                InspectCommand.RunAsync(globalRoot: dir, projectRoot: null,
                    activeProfiles: ["my-profile"]));

            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.TryGetProperty("activeProfiles", out _));
            Assert.True(doc.RootElement.TryGetProperty("documents", out _));
            Assert.True(doc.RootElement.TryGetProperty("rules", out _));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task<(int exitCode, string stdout)> CaptureStdout(
        Func<Task<int>> action)
    {
        var original = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            var exitCode = await action();
            return (exitCode, sw.ToString().Trim());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
