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

        var docMap = new Dictionary<string, SteeringDocument>(StringComparer.Ordinal);
        foreach (var doc in globalDocuments)
        {
            if (doc.Id is not null)
                docMap[doc.Id] = doc;
        }
        foreach (var doc in projectDocuments)
        {
            if (doc.Id is not null)
                docMap[doc.Id] = doc;
        }

        var sortedDocs = docMap.Values
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToList();

        var ruleMap = new Dictionary<string, SteeringRule>(StringComparer.Ordinal);
        foreach (var doc in sortedDocs)
        {
            foreach (var rule in doc.Rules)
            {
                if (rule.Id is not null)
                    ruleMap[rule.Id] = rule;
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
