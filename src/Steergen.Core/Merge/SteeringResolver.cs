using Steergen.Core.Model;
using Steergen.Core.Packs;

namespace Steergen.Core.Merge;

public sealed class SteeringResolver
{
    /// <summary>
    /// Extended resolve method that accepts rules pack documents with scope metadata.
    /// Merge precedence: project-local > project-scoped packs > supplemental > global.
    /// Within the same scope level, declaration order (earlier in list) wins.
    /// </summary>
    public ResolvedSteeringModel Resolve(
        IEnumerable<SteeringDocument> projectDocuments,
        IReadOnlyList<ScopedPackDocuments> packDocuments,
        IEnumerable<string> activeProfiles)
    {
        throw new NotImplementedException(
            "Rules merge with scope-based precedence is not yet implemented. " +
            "This will be implemented in task 7.6.");
    }

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
