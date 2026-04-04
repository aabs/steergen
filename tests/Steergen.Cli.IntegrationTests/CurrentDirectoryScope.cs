namespace Steergen.Cli.IntegrationTests;

internal sealed class CurrentDirectoryScope : IDisposable
{
    private readonly string _originalDirectory;

    public CurrentDirectoryScope(string directory)
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(directory);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
    }
}