namespace Steergen.Core.Configuration;

public sealed class ConfigWriteConflictException : Exception
{
    public ConfigWriteConflictException(string message) : base(message) { }
    public ConfigWriteConflictException(string message, Exception innerException) : base(message, innerException) { }
}
