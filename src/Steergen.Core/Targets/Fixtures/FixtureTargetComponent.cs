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

    public async Task GenerateAsync(
        ResolvedSteeringModel model,
        TargetConfiguration config,
        CancellationToken cancellationToken)
    {
        var outputPath = config.OutputPath
            ?? throw new InvalidOperationException("Fixture target requires OutputPath to be set.");

        Directory.CreateDirectory(outputPath);

        var lines = model.Rules
            .Where(r => !r.Deprecated)
            .Select(r => r.Id ?? "(no-id)")
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var manifestPath = Path.Combine(outputPath, "fixture-manifest.txt");
        await File.WriteAllLinesAsync(manifestPath, lines, cancellationToken);
    }
}
