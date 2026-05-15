namespace Steergen.Core.Model;

/// <summary>
/// Declarative filter over steering rule metadata. Fields are ORed within a field
/// and ANDed across fields. A wildcard value (<c>"*"</c>) matches any non-null value.
/// An empty or absent field matches any value (no constraint).
/// </summary>
public record RouteMatchExpression
{
    public IReadOnlyList<string> TagsAny { get; init; } = [];
    public IReadOnlyList<string> Category { get; init; } = [];
    public bool? Mandatory { get; init; } = null;
    public IReadOnlyDictionary<string, string> SourceContext { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Returns true when the expression has no constraints (matches everything).
    /// </summary>
    public bool IsEmpty =>
        TagsAny.Count == 0 &&
        Category.Count == 0 &&
        Mandatory is null &&
        SourceContext.Count == 0;
}
