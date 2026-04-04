namespace Steergen.Cli.Commands;

internal static class ConfigPathResolver
{
    public const string DefaultFileName = "steergen.config.yaml";

    public static string DefaultPathForCurrentDirectory() =>
        Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);

    public static string DefaultPathForDirectory(string directory) =>
        Path.Combine(directory, DefaultFileName);

    public static string? ResolveOptional(string? configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
            return configPath;

        var defaultPath = DefaultPathForCurrentDirectory();
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    public static string ResolveRequired(string? configPath) =>
        string.IsNullOrWhiteSpace(configPath)
            ? DefaultPathForCurrentDirectory()
            : configPath;
}