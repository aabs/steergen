using Steergen.Core.Model;

namespace Steergen.Core.Validation;

public enum DiagnosticSeverity { Error, Warning, Info }

public record Diagnostic(
    string Code,
    string Message,
    DiagnosticSeverity Severity,
    SourceLocation? Location = null);
