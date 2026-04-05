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
    /// Builds a <see cref="WritePlan"/> for <paramref name="targetId"/> from
    /// <paramref name="resolutions"/>. Unresolved entries are excluded.
    /// Destination files are ordered alphabetically by path for stable output.
    /// </summary>
    public WritePlan Build(
        string targetId,
        IReadOnlyList<RouteResolutionResult> resolutions)
    {
        var files = resolutions
            .Where(r => r.IsResolved && r.SelectedDestinationPath is not null)
            .GroupBy(r => r.SelectedDestinationPath!, StringComparer.Ordinal)
            .Select(group => BuildFile(group.Key, group.ToList()))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        return new WritePlan { TargetId = targetId, Files = files };
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
