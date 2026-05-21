using Steergen.Core.Model;
using Steergen.Core.Packs;
using Steergen.Core.Validation;

namespace Steergen.Core.Merge;

public sealed class SteeringResolver
{
    /// <summary>
    /// Extended resolve method that accepts rules pack documents with scope metadata.
    /// Merge precedence: project-local > project-scoped packs > supplemental > global.
    /// Within the same scope level, declaration order (earlier in list) wins.
    /// Duplicate rule IDs at the same scope emit a warning diagnostic (RP004).
    /// </summary>
    public ResolvedSteeringModel Resolve(
        IEnumerable<SteeringDocument> projectDocuments,
        IReadOnlyList<ScopedPackDocuments> packDocuments,
        IEnumerable<string> activeProfiles)
    {
        var profiles = activeProfiles.ToList();
        var diagnostics = new List<Diagnostic>();

        // Collect all documents in merge order for the SourceIndex and Documents output.
        var allDocuments = new List<SteeringDocument>();

        // ── Phase 1: Build the rule map with scope-based precedence ──────────────
        // We process from lowest to highest precedence so that higher-precedence
        // rules overwrite lower-precedence ones in the map.
        // Precedence (lowest to highest): Global(1) < Supplemental(2) < Project-scoped packs(3) < Project-local(4)

        // Track which rule IDs have been seen at each scope level for duplicate detection.
        // Key: (ruleId, scopeLevel), Value: source description (pack name or "project-local")
        var seenAtScope = new Dictionary<(string RuleId, int ScopeLevel), string>();

        var ruleMap = new Dictionary<string, (SteeringRule Rule, int Precedence)>(StringComparer.Ordinal);

        // Process pack documents grouped by scope, from lowest to highest precedence.
        // Within each scope group, documents maintain their declaration order (earlier wins).
        var scopeOrder = new[] { PackScope.Global, PackScope.Supplemental, PackScope.Project };

        foreach (var scope in scopeOrder)
        {
            var scopeLevel = ScopePrecedence(scope);

            // Find all ScopedPackDocuments entries matching this scope, preserving declaration order.
            foreach (var scopedPack in packDocuments.Where(p => p.Scope == scope))
            {
                foreach (var doc in scopedPack.Documents)
                {
                    allDocuments.Add(doc);

                    foreach (var rule in doc.Rules)
                    {
                        if (rule.Id is null)
                            continue;

                        var scopeKey = (rule.Id, scopeLevel);

                        if (seenAtScope.TryGetValue(scopeKey, out var existingSource))
                        {
                            // Duplicate rule ID at the same scope level — emit warning.
                            // The earlier declaration already won, so we skip this one.
                            var currentSource = rule.SourcePackName ?? "unknown";
                            diagnostics.Add(new Diagnostic(
                                "RP004",
                                $"Duplicate rule ID '{rule.Id}' at scope '{scope}': " +
                                $"already declared by '{existingSource}', ignoring from '{currentSource}'.",
                                DiagnosticSeverity.Warning));
                            continue;
                        }

                        seenAtScope[scopeKey] = rule.SourcePackName ?? "unknown";

                        // Apply precedence: only overwrite if this scope is higher or equal
                        // (but within same scope, first wins — so we only write if not already present at same level)
                        if (!ruleMap.TryGetValue(rule.Id, out var existing) || scopeLevel > existing.Precedence)
                        {
                            var stem = doc.SourcePath is not null
                                ? Path.GetFileNameWithoutExtension(doc.SourcePath)
                                : doc.Id;

                            ruleMap[rule.Id] = (rule with
                            {
                                InputFileStem = stem,
                                SourceScope = MapPackScopeToRouteScope(scope)
                            }, scopeLevel);
                        }
                    }
                }
            }
        }

        // Process project-local documents (highest precedence = 4)
        var projectList = projectDocuments.ToList();
        foreach (var doc in projectList)
        {
            allDocuments.Add(doc);

            var stem = doc.SourcePath is not null
                ? Path.GetFileNameWithoutExtension(doc.SourcePath)
                : doc.Id;

            foreach (var rule in doc.Rules)
            {
                if (rule.Id is null)
                    continue;

                // Project-local always wins (precedence 4), overwrite unconditionally
                ruleMap[rule.Id] = (rule with
                {
                    InputFileStem = stem,
                    SourceScope = RouteScope.Project
                }, 4);
            }
        }

        // ── Phase 2: Build output ────────────────────────────────────────────────

        var sortedRules = ruleMap.Values
            .Select(v => v.Rule)
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var sortedDocs = allDocuments
            .Where(d => d.Id is not null)
            .DistinctBy(d => d.Id, StringComparer.Ordinal)
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var sourceIndex = sortedDocs
            .ToDictionary(d => d.Id!, StringComparer.Ordinal);

        return new ResolvedSteeringModel
        {
            Documents = sortedDocs,
            Rules = sortedRules,
            ActiveProfiles = profiles,
            SourceIndex = sourceIndex,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>
    /// Returns the numeric precedence for a PackScope (higher = wins).
    /// </summary>
    private static int ScopePrecedence(PackScope scope) => scope switch
    {
        PackScope.Global => 1,
        PackScope.Supplemental => 2,
        PackScope.Project => 3,
        _ => 0
    };

    /// <summary>
    /// Maps a PackScope to the corresponding RouteScope for rule tagging.
    /// </summary>
    private static RouteScope MapPackScopeToRouteScope(PackScope scope) => scope switch
    {
        PackScope.Global => RouteScope.Global,
        PackScope.Project => RouteScope.Project,
        PackScope.Supplemental => RouteScope.Both, // Supplemental maps to Both (between global and project)
        _ => RouteScope.Both
    };

    public ResolvedSteeringModel Resolve(
        IEnumerable<SteeringDocument> globalDocuments,
        IEnumerable<SteeringDocument> projectDocuments,
        IEnumerable<string> activeProfiles)
    {
        var profiles = activeProfiles.ToList();
        var globalList = globalDocuments.ToList();
        var projectList = projectDocuments.ToList();

        var docMap = new Dictionary<string, SteeringDocument>(StringComparer.Ordinal);
        var sourceScopes = new Dictionary<string, RouteScope>(StringComparer.Ordinal);
        foreach (var doc in globalList)
        {
            if (doc.Id is not null)
            {
                docMap[doc.Id] = doc;
                sourceScopes[doc.Id] = RouteScope.Global;
            }
        }
        foreach (var doc in projectList)
        {
            if (doc.Id is not null)
            {
                docMap[doc.Id] = doc;
                sourceScopes[doc.Id] = RouteScope.Project;
            }
        }

        var sortedDocs = docMap.Values
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var ruleMap = new Dictionary<string, SteeringRule>(StringComparer.Ordinal);
        foreach (var doc in sortedDocs)
        {
            var stem = doc.SourcePath is not null
                ? Path.GetFileNameWithoutExtension(doc.SourcePath)
                : doc.Id;
            var sourceScope = doc.Id is not null && sourceScopes.TryGetValue(doc.Id, out var resolvedScope)
                ? resolvedScope
                : RouteScope.Both;

            foreach (var rule in doc.Rules)
            {
                if (rule.Id is not null)
                    ruleMap[rule.Id] = rule with { InputFileStem = stem, SourceScope = sourceScope };
            }
        }

        var filteredRules = ruleMap.Values
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();

        var sourceIndex = sortedDocs
            .Where(d => d.Id is not null)
            .ToDictionary(d => d.Id!, StringComparer.Ordinal);

        return new ResolvedSteeringModel
        {
            Documents = sortedDocs,
            Rules = filteredRules,
            ActiveProfiles = profiles,
            SourceIndex = sourceIndex,
        };
    }
}
