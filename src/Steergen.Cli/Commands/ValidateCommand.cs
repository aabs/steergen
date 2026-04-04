using System.CommandLine;
using Steergen.Core.Parsing;
using Steergen.Core.Validation;

namespace Steergen.Cli.Commands;

/// <summary>
/// Validates one or more steering document directories and reports diagnostics.
/// Exits with code 0 (no errors), 1 (validation errors found), or 2 (configuration/IO error).
/// </summary>
public static class ValidateCommand
{
    public static Command Create()
    {
        var globalOption = new Option<string?>("--global")
        {
            Description = "Path to the global steering documents directory",
        };
        var projectOption = new Option<string?>("--project")
        {
            Description = "Path to the project steering documents directory",
        };
        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress informational output; only emit errors",
        };

        var cmd = new Command("validate", "Validate steering documents")
        {
            globalOption,
            projectOption,
            quietOption,
        };

        cmd.SetAction(async (parseResult, cancellationToken) =>
        {
            var globalRoot = parseResult.GetValue(globalOption);
            var projectRoot = parseResult.GetValue(projectOption);
            var quiet = parseResult.GetValue(quietOption);

            return await RunAsync(globalRoot, projectRoot, quiet, cancellationToken);
        });

        return cmd;
    }

    public static async Task<int> RunAsync(
        string? globalRoot,
        string? projectRoot,
        bool quiet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allDocuments = new List<Core.Model.SteeringDocument>();

            if (globalRoot is not null)
            {
                if (!Directory.Exists(globalRoot))
                {
                    Console.Error.WriteLine($"[error] Global directory not found: {globalRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                allDocuments.AddRange(LoadDocuments(globalRoot));
            }

            if (projectRoot is not null)
            {
                if (!Directory.Exists(projectRoot))
                {
                    Console.Error.WriteLine($"[error] Project directory not found: {projectRoot}");
                    return Composition.ExitCodeMapper.ConfigurationError;
                }
                allDocuments.AddRange(LoadDocuments(projectRoot));
            }

            if (allDocuments.Count == 0 && !quiet)
            {
                Console.Error.WriteLine("[warning] No steering documents found. Provide --global or --project.");
            }

            var validator = new SteeringValidator();
            var diagnostics = validator.ValidateCorpus(allDocuments);

            int errorCount = 0;
            int warningCount = 0;

            foreach (var diag in diagnostics)
            {
                var severity = diag.Severity switch
                {
                    DiagnosticSeverity.Error => "error",
                    DiagnosticSeverity.Warning => "warning",
                    _ => "info",
                };

                if (diag.Severity == DiagnosticSeverity.Error)
                    errorCount++;
                else if (diag.Severity == DiagnosticSeverity.Warning)
                    warningCount++;

                if (diag.Severity == DiagnosticSeverity.Error || !quiet)
                {
                    var lineInfo = diag.Location is not null ? $"({diag.Location.LineNumber})" : string.Empty;
                    var location = diag.Location is not null ? $"{diag.Location.FilePath}{lineInfo}: " : string.Empty;
                    Console.Error.WriteLine($"{location}[{severity}] {diag.Code}: {diag.Message}");
                }
            }

            if (!quiet)
            {
                Console.Error.WriteLine($"Validation complete: {errorCount} error(s), {warningCount} warning(s).");
            }

            await Task.CompletedTask;
            return errorCount > 0 ? Composition.ExitCodeMapper.ValidationError : Composition.ExitCodeMapper.Success;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[error] Unexpected error: {ex.Message}");
            return Composition.ExitCodeMapper.ConfigurationError;
        }
    }

    private static IEnumerable<Core.Model.SteeringDocument> LoadDocuments(string root) =>
        Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(path => SteeringMarkdownParser.Parse(File.ReadAllText(path), path));
}
