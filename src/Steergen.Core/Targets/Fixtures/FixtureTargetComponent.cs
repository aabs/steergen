using Steergen.Core.Model;

namespace Steergen.Core.Targets.Fixtures;

/// <summary>
/// Minimal fixture target that proves the additive extension seam.
/// Writes a sorted list of active rule IDs to <c>fixture-manifest.txt</c> in the output directory.
/// No template provider required.
/// </summary>
public sealed class FixtureTargetComponent : ITargetComponent
{
    private static readonly TargetDescriptor FixtureDescriptor =
        new("fixture", "Fixture", "Minimal fixture target for verifying additive target extension seam.");

    public string TargetId => "fixture";
    public TargetDescriptor Descriptor => FixtureDescriptor;

    public async Task GenerateWithPlanAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        WritePlan writePlan,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Fixture target requires OutputPath to be set.");

        var lines = writePlan.Files.Count > 0
            ? ResolveRoutedRuleIds(model, writePlan)
            : model.Rules
                .Where(r => !r.Deprecated)
                .Select(r => r.Id ?? "(no-id)")
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

        Directory.CreateDirectory(outputPath);
        var manifestPath = Path.Combine(outputPath, "fixture-manifest.txt");
        await File.WriteAllLinesAsync(manifestPath, lines, cancellationToken);
    }

    private static List<string> ResolveRoutedRuleIds(ResolvedSteeringModel model, WritePlan writePlan)
    {
        var ruleIndex = model.Rules.ToDictionary(r => r.Id ?? "", StringComparer.Ordinal);

        return writePlan.Files
            .SelectMany(file => file.AppendUnits)
            .Select(unit => ruleIndex.TryGetValue(unit.RuleId, out var rule) ? rule : null)
            .Where(rule => rule is not null && !rule.Deprecated)
            .Select(rule => rule!.Id ?? "(no-id)")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }
}
