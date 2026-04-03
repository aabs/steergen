namespace Steergen.Core.Model;

public record SourceLocation(string FilePath, int LineNumber, int? ColumnNumber = null);
