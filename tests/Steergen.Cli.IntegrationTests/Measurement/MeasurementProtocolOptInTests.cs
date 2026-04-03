using Steergen.Cli.Commands;

namespace Steergen.Cli.IntegrationTests.Measurement;

/// <summary>
/// Integration tests for NFR-016: measurement routines for SC-001/SC-005 MUST only execute
/// when <c>--verbose</c> or <c>--debug</c> is enabled, and MUST remain silent in default mode.
/// All measurement output is routed to stderr.
/// </summary>
[Collection("CliOutput")]
public sealed class MeasurementProtocolOptInTests
{
    private static readonly string FixturesRoot =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "tests", "Fixtures", "RealisticGovernance"));

    private static string MakeTempDir() => Directory.CreateTempSubdirectory("measure-").FullName;

    // ── Default mode: no measurement output ──────────────────────────────

    [Fact]
    public async Task Run_DefaultMode_EmitsNoMeasurementLines()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: true,
                    verbose: false,
                    debug: false,
                    cancellationToken: default));

            Assert.DoesNotContain(stderr, line => line.StartsWith("[measure]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_QuietMode_EmitsNoMeasurementLines()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: true,
                    verbose: false,
                    debug: false,
                    cancellationToken: default));

            Assert.DoesNotContain(stderr, line => line.StartsWith("[measure]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Verbose mode: measurement output appears ─────────────────────────

    [Fact]
    public async Task Run_VerboseMode_EmitsMeasurementLines()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default));

            Assert.Contains(stderr, line => line.StartsWith("[measure]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_DebugMode_EmitsMeasurementLines()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: false,
                    debug: true,
                    cancellationToken: default));

            Assert.Contains(stderr, line => line.StartsWith("[measure]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Measurement content: expected operation names ────────────────────

    [Fact]
    public async Task Run_VerboseMode_EmitsLoadDocumentsEntry()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default));

            Assert.Contains(stderr, line =>
                line.StartsWith("[measure]", StringComparison.Ordinal) &&
                line.Contains("load-documents", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_VerboseMode_EmitsPipelineEntry()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default));

            Assert.Contains(stderr, line =>
                line.StartsWith("[measure]", StringComparison.Ordinal) &&
                line.Contains("run-pipeline", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_VerboseMode_EmitsTotalEntry()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default));

            Assert.Contains(stderr, line =>
                line.StartsWith("[measure]", StringComparison.Ordinal) &&
                line.Contains("total", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    [Fact]
    public async Task Run_VerboseMode_MeasurementLinesContainMilliseconds()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stderr = await CaptureStderr(() =>
                RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default));

            var measureLines = stderr.Where(l => l.StartsWith("[measure]", StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(measureLines);
            Assert.All(measureLines, line => Assert.Contains("ms", line, StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Stderr vs stdout isolation ────────────────────────────────────────

    [Fact]
    public async Task Run_VerboseMode_MeasurementGoesToStderr_NotStdout()
    {
        var outputDir = MakeTempDir();
        try
        {
            var stdoutLines = new List<string>();
            var stdoutWriter = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(stdoutWriter);

            try
            {
                await RunCommand.RunAsync(
                    configPath: null,
                    globalRoot: Path.Combine(FixturesRoot, "global"),
                    projectRoot: null,
                    outputBase: outputDir,
                    explicitTargets: ["speckit"],
                    quiet: false,
                    verbose: true,
                    debug: false,
                    cancellationToken: default);

                stdoutLines.AddRange(stdoutWriter.ToString()
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            Assert.DoesNotContain(stdoutLines, l => l.StartsWith("[measure]", StringComparison.Ordinal));
        }
        finally { Directory.Delete(outputDir, recursive: true); }
    }

    // ── Helper: capture stderr lines ─────────────────────────────────────

    private static async Task<IReadOnlyList<string>> CaptureStderr(Func<Task> work)
    {
        using var sw = new StringWriter();
        var original = Console.Error;
        Console.SetError(sw);
        try
        {
            await work();
        }
        finally
        {
            Console.SetError(original);
        }
        return sw.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
