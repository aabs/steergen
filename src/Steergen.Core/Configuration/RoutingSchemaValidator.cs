using Steergen.Core.Model;
using Steergen.Core.Validation;

namespace Steergen.Core.Configuration;

/// <summary>
/// Validates a <see cref="TargetLayoutDefinition"/> for structural correctness:
/// non-empty routes, unique IDs, core anchor presence, and safe destination paths.
/// Returns zero or more <see cref="Diagnostic"/> entries; an empty list means valid.
/// </summary>
public sealed class RoutingSchemaValidator
{
    /// <summary>
    /// Validates the supplied <paramref name="layout"/> and returns diagnostics.
    /// Returns an empty list when the layout is structurally valid.
    /// </summary>
    public IReadOnlyList<Diagnostic> Validate(TargetLayoutDefinition layout)
    {
        var diagnostics = new List<Diagnostic>();

        CheckRoutesNotEmpty(layout, diagnostics);
        CheckDuplicateRouteIds(layout, diagnostics);
        CheckCoreAnchorPresent(layout, diagnostics);
        CheckDestinationPathSafety(layout, diagnostics);

        return diagnostics
            .OrderBy(d => d.Code, StringComparer.Ordinal)
            .ToList();
    }

    // ── RS001: Routes non-empty ───────────────────────────────────────────────

    private static void CheckRoutesNotEmpty(TargetLayoutDefinition layout, List<Diagnostic> diagnostics)
    {
        if (layout.Routes.Count == 0)
            diagnostics.Add(new Diagnostic(
                "RS001",
                $"Target '{layout.TargetId}': layout has no routes defined. At least one route is required.",
                DiagnosticSeverity.Error));
    }

    // ── RS002: Unique route IDs ───────────────────────────────────────────────

    private static void CheckDuplicateRouteIds(TargetLayoutDefinition layout, List<Diagnostic> diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in layout.Routes)
        {
            if (!seen.Add(route.Id))
                diagnostics.Add(new Diagnostic(
                    "RS002",
                    $"Target '{layout.TargetId}': duplicate route id '{route.Id}'.",
                    DiagnosticSeverity.Error));
        }
    }

    // ── RS003: Core anchor present ────────────────────────────────────────────

    private static void CheckCoreAnchorPresent(TargetLayoutDefinition layout, List<Diagnostic> diagnostics)
    {
        if (layout.Routes.Count == 0) return; // RS001 already covers this

        var hasCoreAnchor = layout.Routes.Any(r => r.Anchor == RouteAnchor.Core);
        if (!hasCoreAnchor)
            diagnostics.Add(new Diagnostic(
                "RS003",
                $"Target '{layout.TargetId}': no core-anchor route found. " +
                "At least one route must have anchor: core to support fallback behavior.",
                DiagnosticSeverity.Error));
    }

    // ── RS004/RS005/RS006: Destination path safety ────────────────────────────

    private static void CheckDestinationPathSafety(TargetLayoutDefinition layout, List<Diagnostic> diagnostics)
    {
        foreach (var route in layout.Routes)
        {
            var dest = route.Destination;

            CheckDirectoryTraversal(layout.TargetId, route.Id, dest.Directory, diagnostics);
            CheckDirectoryAbsolute(layout.TargetId, route.Id, dest.Directory, diagnostics);
            CheckFileNamePathSeparator(layout.TargetId, route.Id, dest.FileName, diagnostics);
        }
    }

    private static void CheckDirectoryTraversal(
        string targetId, string routeId, string directory, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(directory)) return;

        // Normalize variable tokens so ${x} doesn't confuse the traversal check.
        var expanded = directory.Replace("${", "").Replace("}", "");

        // Split on both forward slash and backslash, then look for ".." segments.
        var segments = expanded.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".."))
            diagnostics.Add(new Diagnostic(
                "RS004",
                $"Target '{targetId}', route '{routeId}': destination directory '{directory}' " +
                "contains path traversal ('..') which is not allowed.",
                DiagnosticSeverity.Error));
    }

    private static void CheckDirectoryAbsolute(
        string targetId, string routeId, string directory, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(directory)) return;
        if (directory.StartsWith("${", StringComparison.Ordinal)) return; // variable-prefixed templates are ok

        // Detect absolute paths on both Unix (/...) and Windows (C:\..., C:/...).
        var isAbsolute = Path.IsPathRooted(directory)
            || (directory.Length >= 3 && char.IsLetter(directory[0]) && directory[1] == ':' &&
                (directory[2] == '\\' || directory[2] == '/'));

        if (isAbsolute)
            diagnostics.Add(new Diagnostic(
                "RS005",
                $"Target '{targetId}', route '{routeId}': destination directory '{directory}' " +
                "is an absolute path. Only relative paths and variable templates are allowed.",
                DiagnosticSeverity.Error));
    }

    private static void CheckFileNamePathSeparator(
        string targetId, string routeId, string fileName, List<Diagnostic> diagnostics)
    {
        if (string.IsNullOrEmpty(fileName)) return;

        // Strip variable tokens before checking for separators
        var expanded = fileName.Replace("${", "").Replace("}", "");
        if (expanded.Contains('/') || expanded.Contains('\\'))
            diagnostics.Add(new Diagnostic(
                "RS006",
                $"Target '{targetId}', route '{routeId}': destination fileName '{fileName}' " +
                "must not contain path separators ('/' or '\\\\').",
                DiagnosticSeverity.Error));
    }
}
