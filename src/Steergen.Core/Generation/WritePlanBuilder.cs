using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Builds a deterministic <see cref="WritePlan"/> from a set of
/// <see cref="RouteResolutionResult"/> entries produced by <see cref="RoutePlanner"/>.
///
/// Rules resolved to the same destination path are grouped into a single
/// <see cref="WritePlanFile"/>; content units within each file are ordered
/// deterministically by rule ID.
/// </summary>
public sealed class WritePlanBuilder
{
    /// <summary>
    /// Fallback destination used when a resolution remains unresolved even after
    /// <see cref="RoutePlanner"/> has attempted its own fallback strategy (e.g.
    /// when the layout has no core-anchor route).  Every such rule is collected
    /// into a single <c>other.md</c> file so no rule is silently dropped.
    /// </summary>
    public const string FallbackOtherFile = "other.md";

    /// <summary>
    /// Builds a <see cref="WritePlan"/> for <paramref name="targetId"/> from
    /// <paramref name="resolutions"/>. Any entries that remain unresolved (i.e.
    /// <see cref="RouteResolutionResult.IsResolved"/> is <c>false</c>) are
    /// collected into a top-level <c>other.md</c> file so that no rule is ever
    /// silently dropped.  Destination files are ordered alphabetically by path
    /// for stable output.
    /// </summary>
    public WritePlan Build(
        string targetId,
        IReadOnlyList<RouteResolutionResult> resolutions)
    {
        var files = new List<WritePlanFile>();
        var unresolvedGroup = new List<RouteResolutionResult>();
        var resolvedByPath = new Dictionary<string, List<RouteResolutionResult>>(StringComparer.Ordinal);

        foreach (var r in resolutions)
        {
            if (r.IsResolved && r.SelectedDestinationPath is not null)
            {
                if (!resolvedByPath.TryGetValue(r.SelectedDestinationPath, out var group))
                    resolvedByPath[r.SelectedDestinationPath] = group = [];
                group.Add(r);
            }
            else
            {
                unresolvedGroup.Add(r);
            }
        }

        foreach (var (path, group) in resolvedByPath)
            files.Add(BuildFile(path, group));

        // Rules that remain unresolved (e.g. layout has no core-anchor route)
        // are collected into a catch-all other.md so they are never silently lost.
        if (unresolvedGroup.Count > 0)
            files.Add(BuildFile(FallbackOtherFile, unresolvedGroup));

        return new WritePlan
        {
            TargetId = targetId,
            Files = files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
        };
    }

    private static WritePlanFile BuildFile(string path, List<RouteResolutionResult> resolutions)
    {
        var units = resolutions
            .OrderBy(r => r.RuleId, StringComparer.Ordinal)
            .Select((r, i) => new ContentUnit
            {
                RuleId = r.RuleId,
                RenderedContent = "",   // content rendered during plan execution, not at planning time
                OrderKey = (0, i, r.RuleId),
            })
            .ToList();

        return new WritePlanFile
        {
            Path = path,
            TruncateAtStart = true,
            AppendUnits = units,
        };
    }
}
