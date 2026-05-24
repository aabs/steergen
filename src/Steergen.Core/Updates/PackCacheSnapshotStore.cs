namespace Steergen.Core.Updates;

public class PackCacheSnapshotStore
{
    public virtual async Task<string?> CaptureAsync(string cachePath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(cachePath))
            return null;

        var snapshotPath = Path.Combine(Path.GetTempPath(), $"steergen-cache-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(snapshotPath);

        await CopyDirectoryAsync(cachePath, snapshotPath, cancellationToken).ConfigureAwait(false);
        return snapshotPath;
    }

    public virtual async Task RestoreAsync(string snapshotPath, string cachePath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(snapshotPath))
            return;

        if (Directory.Exists(cachePath))
            Directory.Delete(cachePath, recursive: true);

        Directory.CreateDirectory(cachePath);
        await CopyDirectoryAsync(snapshotPath, cachePath, cancellationToken).ConfigureAwait(false);
    }

    public virtual void DeleteSnapshot(string? snapshotPath)
    {
        if (string.IsNullOrWhiteSpace(snapshotPath) || !Directory.Exists(snapshotPath))
            return;

        Directory.Delete(snapshotPath, recursive: true);
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destinationDir, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            await using var sourceStream = File.OpenRead(file);
            await using var destinationStream = File.Create(destination);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
        }
    }
}
