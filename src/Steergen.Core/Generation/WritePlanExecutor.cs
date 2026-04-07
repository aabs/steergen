using Steergen.Core.Model;

namespace Steergen.Core.Generation;

/// <summary>
/// Executes a <see cref="WritePlan"/> by truncating destination files at the start of the run,
/// then appending ordered content units. Produces a deterministic lifecycle report.
/// Files are processed in alphabetical path order for stable output.
/// </summary>
public sealed class WritePlanExecutor
{
    /// <summary>
    /// Executes the write plan: truncates each destination file, then writes all content units
    /// in deterministic order. Returns a <see cref="WritePlanReport"/> describing what was written.
    /// </summary>
    public async Task<WritePlanReport> ExecuteAsync(
        WritePlan plan,
        CancellationToken cancellationToken = default)
    {
        var writtenFiles = new List<string>();
        var truncatedFiles = new List<string>();

        foreach (var file in plan.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var dir = Path.GetDirectoryName(file.Path);
                if (dir is not null && dir.Length > 0)
                    Directory.CreateDirectory(dir);

                if (file.TruncateAtStart && File.Exists(file.Path))
                    truncatedFiles.Add(file.Path);

                var content = string.Concat(
                    file.AppendUnits
                        .OrderBy(u => u.OrderKey)
                        .Select(u => u.RenderedContent));

                await File.WriteAllTextAsync(file.Path, content, cancellationToken)
                    .ConfigureAwait(false);

                writtenFiles.Add(file.Path);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new WritePlanReport
                {
                    TargetId = plan.TargetId,
                    WrittenFiles = writtenFiles,
                    TruncatedFiles = truncatedFiles,
                    Success = false,
                    FailureReason = $"Failed to write '{file.Path}': {ex.Message}",
                };
            }
        }

        return new WritePlanReport
        {
            TargetId = plan.TargetId,
            WrittenFiles = writtenFiles,
            TruncatedFiles = truncatedFiles,
            Success = true,
        };
    }
}
