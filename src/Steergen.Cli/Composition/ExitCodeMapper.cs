using Steergen.Core.Configuration;
using Steergen.Core.Generation;
using Steergen.Core.Updates;

namespace Steergen.Cli.Composition;

public static class ExitCodeMapper
{
    public const int Success = 0;
    public const int ValidationError = 1;
    public const int ConfigurationError = 2;
    public const int GenerationError = 3;
    public const int ConflictError = 5;
    public const int UpgradeValidationError = 6;
    public const int UpgradeExecutionError = 7;
    public const int UpgradeRollbackError = 8;

    public static int FromException(Exception ex)
    {
        return ex switch
        {
            ConfigWriteConflictException => ConflictError,
            TargetGenerationException => GenerationError,
            InvalidOperationException => ConfigurationError,
            _ => GenerationError,
        };
    }

    public static int FromUpgradeResult(ExternalPackUpgradeResult result)
    {
        if (result.Success)
            return Success;

        if (result.Diagnostics.Any(d => string.Equals(d.Code, "UPG002", StringComparison.Ordinal)))
            return UpgradeRollbackError;

        if (string.IsNullOrWhiteSpace(result.ErrorMessage))
            return UpgradeExecutionError;

        if (result.ErrorMessage.Contains("selector", StringComparison.OrdinalIgnoreCase)
            || result.ErrorMessage.Contains("format", StringComparison.OrdinalIgnoreCase))
        {
            return UpgradeValidationError;
        }

        return UpgradeExecutionError;
    }
}
