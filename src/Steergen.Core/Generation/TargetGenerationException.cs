namespace Steergen.Core.Generation;

public sealed class TargetGenerationException : Exception
{
    public string MissingKey { get; }

    public TargetGenerationException(string missingKey)
        : base($"Required metadata key '{missingKey}' is not present in FormatOptions.")
    {
        MissingKey = missingKey;
    }
}
