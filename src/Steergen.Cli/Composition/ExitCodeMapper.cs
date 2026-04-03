using Steergen.Core.Configuration;

namespace Steergen.Cli.Composition;

public static class ExitCodeMapper
{
    public const int Success = 0;
    public const int ValidationError = 1;
    public const int ConfigurationError = 2;
    public const int GenerationError = 3;
    public const int ConflictError = 5;

    public static int FromException(Exception ex)
    {
        return ex switch
        {
            ConfigWriteConflictException => ConflictError,
            InvalidOperationException => ConfigurationError,
            _ => GenerationError,
        };
    }
}
