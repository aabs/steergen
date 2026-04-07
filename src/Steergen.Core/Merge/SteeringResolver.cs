using Steergen.Core.Model;

namespace Steergen.Core.Merge;

public sealed class SteeringResolver
{
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
            .Where(r => profiles.Count == 0 || r.Profile is null || profiles.Contains(r.Profile))
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
